using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Threading;
using BrewAlert.Core.Events;
using BrewAlert.Core.Interfaces;
using BrewAlert.Core.Models;
using BrewAlert.Core.Services;
using BrewAlert.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace BrewAlert.UI.ViewModels;

/// <summary>
/// Dedicated personalized brew view shown after an operator scans their RFID card
/// or signs in without a card. Handles first-time setup, custom duration editing,
/// and one-tap personalized brew startup.
/// </summary>
public partial class OperatorBrewViewModel : ViewModelBase, IRfidCardScanAwaiter, IDisposable
{
    public enum ViewMode
    {
        SetupPrompt,
        BrewSelection,
        EditingDurations,
    }

    private static readonly TimeSpan InactivityTimeout = TimeSpan.FromSeconds(30);

    private readonly BrewProfileService _profileService;
    private readonly RfidUserService _rfidUserService;
    private readonly IBrewTimerService _timerService;
    private readonly IRfidReaderService _rfidReader;
    private readonly INavigationService _navigation;
    private readonly ILocalizationService _loc;
    private readonly ILogger<OperatorBrewViewModel> _logger;
    private readonly DispatcherTimer _inactivityTimer;
    private readonly CancellationTokenSource _disposeCts = new();

    private RfidUser? _operator;
    private BrewProfile? _pendingProfile;
    private IReadOnlyList<BrewProfile> _loadedProfiles = [];
    private int _sessionGeneration;
    private bool _disposed;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSetupPrompt))]
    [NotifyPropertyChangedFor(nameof(IsBrewSelection))]
    [NotifyPropertyChangedFor(nameof(IsEditingDurations))]
    private ViewMode _mode = ViewMode.BrewSelection;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GreetingTitle))]
    [NotifyPropertyChangedFor(nameof(EditDurationsTitle))]
    private string _operatorName = string.Empty;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusMessage = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SaveDurationsText))]
    private bool _hasPendingProfile;

    [ObservableProperty] private string _pendingProfileName = string.Empty;

    public bool IsSetupPrompt => Mode == ViewMode.SetupPrompt;
    public bool IsBrewSelection => Mode == ViewMode.BrewSelection;
    public bool IsEditingDurations => Mode == ViewMode.EditingDurations;

    // Never hijack RFID scans for "register card" here; instead, let another user tap
    // their card on the greeting/setup screen to switch operators seamlessly.
    public bool IsAwaitingCardScan => false;
    public bool CanAcceptKeyboardCardScan => !_disposed && Mode != ViewMode.EditingDurations;

    public ObservableCollection<OperatorProfileChoiceItem> ProfileChoices { get; } = [];
    public ObservableCollection<OperatorProfileDurationItem> EditableDurations { get; } = [];
    public ObservableCollection<string> DefaultPreviewPills { get; } = [];

    public string GreetingEyebrow => _loc.Get("OperatorGreetingEyebrow");
    public string GreetingTitle => string.Format(_loc.CurrentCulture, _loc.Get("OperatorGreetingTitle"), OperatorName);
    public string GreetingSubtitle => _loc.Get("OperatorGreetingSubtitle");
    public string BackHomeText => _loc.Get("OperatorBackHome");
    public string EditDurationsText => _loc.Get("OperatorEditDurations");
    public string SetupTitle => _loc.Get("OperatorSetupTitle");
    public string SetupSubtitle => _loc.Get("OperatorSetupSubtitle");
    public string UseDefaultsText => _loc.Get("OperatorUseDefaults");
    public string DefineCustomText => _loc.Get("OperatorDefineCustom");
    public string EditDurationsTitle => string.Format(_loc.CurrentCulture, _loc.Get("OperatorEditTitle"), OperatorName);
    public string EditDurationsSubtitle => _loc.Get("OperatorEditSubtitle");
    public string SaveDurationsText => _loc.Get(HasPendingProfile ? "OperatorSaveAndBrew" : "OperatorSaveDurations");
    public string ResetDurationsText => _loc.Get("OperatorResetDurations");
    public string CancelText => _loc.Get("CancelButton");

    public OperatorBrewViewModel(
        BrewProfileService profileService,
        RfidUserService rfidUserService,
        IBrewTimerService timerService,
        IRfidReaderService rfidReader,
        INavigationService navigation,
        ILocalizationService loc,
        ILogger<OperatorBrewViewModel> logger)
    {
        _profileService = profileService;
        _rfidUserService = rfidUserService;
        _timerService = timerService;
        _rfidReader = rfidReader;
        _navigation = navigation;
        _loc = loc;
        _logger = logger;

        _inactivityTimer = new DispatcherTimer { Interval = InactivityTimeout };
        _inactivityTimer.Tick += OnInactivityTick;

        _rfidReader.CardScanned += OnCardScanned;
        _loc.LanguageChanged += OnLanguageChanged;
    }

    public void Initialize(RfidUser operatorUser, BrewProfile? pendingProfile = null)
    {
        ArgumentNullException.ThrowIfNull(operatorUser);

        var generation = ++_sessionGeneration;
        _operator = operatorUser;
        _pendingProfile = pendingProfile;
        OperatorName = operatorUser.Name;
        HasPendingProfile = pendingProfile is not null;
        PendingProfileName = pendingProfile?.Name ?? string.Empty;
        StatusMessage = string.Empty;
        ProfileChoices.Clear();
        DefaultPreviewPills.Clear();
        EditableDurations.Clear();
        Mode = operatorUser.HasConfiguredProfiles ? ViewMode.BrewSelection : ViewMode.SetupPrompt;

        RestartInactivityTimer();
        _ = LoadProfilesAsync(generation);
    }

    private async Task LoadProfilesAsync(int generation)
    {
        if (_disposed || generation != _sessionGeneration) return;

        IsBusy = true;
        try
        {
            var profiles = await _profileService.GetAllProfilesAsync(_disposeCts.Token);
            if (_disposed || generation != _sessionGeneration) return;

            _loadedProfiles = profiles;
            RebuildProfileProjections();
            RebuildEditableDurations();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load brew profiles for operator view.");
            if (!_disposed && generation == _sessionGeneration)
                StatusMessage = string.Format(_loc.CurrentCulture, _loc.Get("ProfilesLoadError"), ex.Message);
        }
        finally
        {
            if (!_disposed && generation == _sessionGeneration)
                IsBusy = false;
        }
    }

    private void RebuildProfileProjections()
    {
        ProfileChoices.Clear();
        DefaultPreviewPills.Clear();

        var minShort = _loc.Get("MinShort");
        var customBadge = _loc.Get("OperatorCustomBadge");
        var defaultBadge = _loc.Get("OperatorDefaultBadge");

        foreach (var profile in _loadedProfiles)
        {
            var defaultMinutes = ToDisplayMinutes(profile.BrewDuration);
            DefaultPreviewPills.Add($"{profile.Name} · {defaultMinutes} {minShort}");

            var effective = RfidUserService.CreatePersonalizedProfile(profile, _operator);
            var isCustom = RfidUserService.HasCustomDuration(_operator, profile);
            var effectiveMinutes = ToDisplayMinutes(effective.BrewDuration);
            var hint = isCustom
                ? string.Format(_loc.CurrentCulture, _loc.Get("OperatorDefaultDurationHint"), defaultMinutes)
                : string.Empty;

            ProfileChoices.Add(new OperatorProfileChoiceItem(
                SourceProfile: profile,
                EffectiveProfile: effective,
                Name: profile.Name,
                Icon: profile.Icon,
                Description: profile.Description,
                EffectiveDuration: effective.BrewDuration,
                DurationDisplay: $"{effectiveMinutes} {minShort}",
                IsCustomDuration: isCustom,
                BadgeText: isCustom ? customBadge : defaultBadge,
                DefaultHintText: hint));
        }
    }

    private void RebuildEditableDurations()
    {
        EditableDurations.Clear();
        foreach (var profile in _loadedProfiles)
        {
            var defaultMinutes = ToDisplayMinutes(profile.BrewDuration);
            var initialMinutes = ToDisplayMinutes(RfidUserService.ResolveDuration(_operator, profile));
            EditableDurations.Add(new OperatorProfileDurationItem(
                profile.Id,
                profile.Name,
                profile.Icon,
                defaultMinutes,
                initialMinutes,
                _loc,
                RestartInactivityTimer,
                hasCustomOverride: RfidUserService.HasCustomDuration(_operator, profile)));
        }
    }

    [RelayCommand]
    private void SelectProfile(OperatorProfileChoiceItem item)
    {
        if (_disposed || _operator is null || IsBusy || item is null) return;

        _inactivityTimer.Stop();
        _timerService.Start(item.EffectiveProfile, _operator.Name, _operator.CardId);
        _navigation.NavigateTo<ActiveBrewsViewModel>();
    }

    [RelayCommand]
    private async Task UseDefaultsAsync()
    {
        if (_disposed || _operator is null || IsBusy) return;

        var generation = _sessionGeneration;
        var targetOperatorId = _operator.Id;
        var targetOperatorName = _operator.Name;

        IsBusy = true;
        StatusMessage = string.Empty;
        try
        {
            var savedOperator = await _rfidUserService.AcceptDefaultProfilesAsync(targetOperatorId, _disposeCts.Token);
            if (_disposed || generation != _sessionGeneration || _operator?.Id != savedOperator.Id) return;

            _operator = savedOperator;
            if (_pendingProfile is not null)
            {
                StartPendingBrewAndNavigate();
                return;
            }

            RebuildProfileProjections();
            Mode = ViewMode.BrewSelection;
            RestartInactivityTimer();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save default brew profile preference for {OperatorName}.", targetOperatorName);
            if (!_disposed && generation == _sessionGeneration)
                StatusMessage = string.Format(_loc.CurrentCulture, _loc.Get("ErrorPrefix"), ex.Message);
        }
        finally
        {
            if (!_disposed && generation == _sessionGeneration)
                IsBusy = false;
        }
    }

    [RelayCommand]
    private void OpenDurationEditor()
    {
        if (_disposed || _operator is null) return;

        StatusMessage = string.Empty;
        RebuildEditableDurations();
        Mode = ViewMode.EditingDurations;
        RestartInactivityTimer();
    }

    [RelayCommand]
    private async Task SaveDurationsAsync()
    {
        if (_disposed || _operator is null || IsBusy) return;

        var generation = _sessionGeneration;
        var targetOperatorId = _operator.Id;
        var targetOperatorName = _operator.Name;

        var overrides = new Dictionary<Guid, int>();
        foreach (var item in EditableDurations)
        {
            if (item.IsCustomized)
                overrides[item.ProfileId] = item.Minutes;
        }

        IsBusy = true;
        StatusMessage = string.Empty;
        try
        {
            var savedOperator = await _rfidUserService.SaveProfilePreferencesAsync(targetOperatorId, overrides, _disposeCts.Token);
            if (_disposed || generation != _sessionGeneration || _operator?.Id != savedOperator.Id) return;

            _operator = savedOperator;
            if (_pendingProfile is not null)
            {
                StartPendingBrewAndNavigate();
                return;
            }

            RebuildProfileProjections();
            Mode = ViewMode.BrewSelection;
            RestartInactivityTimer();
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save custom brew durations for {OperatorName}.", targetOperatorName);
            if (!_disposed && generation == _sessionGeneration)
                StatusMessage = string.Format(_loc.CurrentCulture, _loc.Get("ErrorPrefix"), ex.Message);
        }
        finally
        {
            if (!_disposed && generation == _sessionGeneration)
                IsBusy = false;
        }
    }

    [RelayCommand]
    private void ResetDurationsToDefault()
    {
        foreach (var item in EditableDurations)
            item.ResetToDefault();
        RestartInactivityTimer();
    }

    [RelayCommand]
    private void CancelDurationEditor()
    {
        if (_disposed || _operator is null) return;

        StatusMessage = string.Empty;
        Mode = _operator.HasConfiguredProfiles ? ViewMode.BrewSelection : ViewMode.SetupPrompt;
        RestartInactivityTimer();
    }

    [RelayCommand]
    private void BackToHome()
    {
        if (_disposed) return;

        _inactivityTimer.Stop();
        _navigation.NavigateTo<ProfileListViewModel>();
    }

    private void StartPendingBrewAndNavigate()
    {
        if (_operator is null || _pendingProfile is null) return;

        var matchedSource = _loadedProfiles.FirstOrDefault(p => p.Id == _pendingProfile.Id) ?? _pendingProfile;
        var personalized = RfidUserService.CreatePersonalizedProfile(matchedSource, _operator);
        _inactivityTimer.Stop();
        _timerService.Start(personalized, _operator.Name, _operator.CardId);
        _navigation.NavigateTo<ActiveBrewsViewModel>();
    }

    private void OnCardScanned(object? sender, RfidCardScannedEvent e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (!_disposed)
                _ = HandleCardScannedAsync(e.CardId);
        });
    }

    private async Task HandleCardScannedAsync(string cardId)
    {
        if (_disposed || Mode == ViewMode.EditingDurations) return;

        var normalized = RfidUserService.NormalizeCardId(cardId);
        if (normalized is null) return;

        try
        {
            var user = await _rfidUserService.FindByCardIdAsync(normalized, _disposeCts.Token);
            if (_disposed || Mode == ViewMode.EditingDurations) return;

            if (user is not null)
            {
                Initialize(user, pendingProfile: null);
            }
            else
            {
                RestartInactivityTimer();
                StatusMessage = string.Format(_loc.CurrentCulture, _loc.Get("RfidUnknownCard"), normalized);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to switch operator by RFID card {CardId}.", normalized);
        }
    }

    private void OnLanguageChanged(string _)
    {
        foreach (var prop in new[]
        {
            nameof(GreetingEyebrow), nameof(GreetingTitle), nameof(GreetingSubtitle),
            nameof(BackHomeText), nameof(EditDurationsText), nameof(SetupTitle),
            nameof(SetupSubtitle), nameof(UseDefaultsText), nameof(DefineCustomText),
            nameof(EditDurationsTitle), nameof(EditDurationsSubtitle), nameof(SaveDurationsText),
            nameof(ResetDurationsText), nameof(CancelText)
        })
        {
            OnPropertyChanged(prop);
        }

        RebuildProfileProjections();
        foreach (var item in EditableDurations)
            item.RefreshLocalization(_loc);
    }

    private void OnInactivityTick(object? sender, EventArgs e) => BackToHome();

    private void RestartInactivityTimer()
    {
        if (_disposed) return;
        _inactivityTimer.Stop();
        _inactivityTimer.Start();
    }

    private static int ToDisplayMinutes(TimeSpan duration) =>
        Math.Max(RfidUserService.MinBrewDurationMinutes, (int)Math.Round(duration.TotalMinutes));

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _inactivityTimer.Stop();
        _inactivityTimer.Tick -= OnInactivityTick;
        _rfidReader.CardScanned -= OnCardScanned;
        _loc.LanguageChanged -= OnLanguageChanged;
        _disposeCts.Cancel();
        _disposeCts.Dispose();
    }
}
