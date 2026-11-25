using System.Collections.ObjectModel;
using System.Globalization;
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
/// Manages operators with optional RFID cards, supporting both card-first registration
/// and cardless operator registration with a built-in touch keyboard.
/// </summary>
public partial class RfidUsersViewModel : ViewModelBase, IRfidCardScanAwaiter, IDisposable
{
    public enum StepMode
    {
        List,
        WaitingForCard,
        EnteringName,
        EditingDurations,
    }

    private readonly RfidUserService _userService;
    private readonly BrewProfileService _profileService;
    private readonly IRfidReaderService _rfidReader;
    private readonly INavigationService _navigation;
    private readonly ILocalizationService _loc;
    private readonly ILogger<RfidUsersViewModel> _logger;
    private readonly CancellationTokenSource _disposeCts = new();
    private Guid? _editingUserId;
    private int _editSessionGeneration;
    private bool _disposed;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsListMode))]
    [NotifyPropertyChangedFor(nameof(IsWaitingForCard))]
    [NotifyPropertyChangedFor(nameof(IsEnteringName))]
    [NotifyPropertyChangedFor(nameof(IsEditingDurations))]
    [NotifyPropertyChangedFor(nameof(IsAwaitingCardScan))]
    private StepMode _step = StepMode.List;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasScannedCard))]
    [NotifyPropertyChangedFor(nameof(CardIdDisplay))]
    [NotifyPropertyChangedFor(nameof(CardActionText))]
    private string _scannedCardId = string.Empty;

    [ObservableProperty] private string _userNameInput = string.Empty;
    [ObservableProperty] private string _existingCardNotice = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private string _editingDurationsTitle = string.Empty;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private bool _hasUsers;

    public ObservableCollection<RfidUser> Users { get; } = [];
    public ObservableCollection<OperatorProfileDurationItem> EditingDurations { get; } = [];

    public bool IsListMode => Step == StepMode.List;
    public bool IsWaitingForCard => Step == StepMode.WaitingForCard;
    public bool IsEnteringName => Step == StepMode.EnteringName;
    public bool IsEditingDurations => Step == StepMode.EditingDurations;
    public bool IsAwaitingCardScan => IsWaitingForCard;
    public bool HasScannedCard => !string.IsNullOrWhiteSpace(ScannedCardId);

    public string Title => _loc.Get("RfidUsersTitle");
    public string BackText => _loc.Get("RfidBackToSettings");
    public string AddUserText => _loc.Get("RfidAddUser");
    public string AddCardlessUserText => _loc.Get("RfidAddCardlessUser");
    public string NoCardBadgeText => _loc.Get("RfidNoCardBadge");
    public string EmptyText => _loc.Get("RfidEmptyUsers");
    public string ScanCardTitle => _loc.Get("RfidScanNewCardTitle");
    public string ScanCardHint => _loc.Get("RfidScanNewCardHint");
    public string ContinueWithoutCardText => _loc.Get("RfidContinueWithoutCard");
    public string CancelText => _loc.Get("CancelButton");
    public string CardActionText => _loc.Get(HasScannedCard ? "RfidRescanCard" : "RfidAssignCard");
    public string RemoveCardText => _loc.Get("RfidRemoveCard");
    public string SaveText => _loc.Get("RfidSaveUser");
    public string EditText => _loc.Get("RfidEditUser");
    public string EditDurationsText => _loc.Get("RfidEditDurations");
    public string EditDurationsSubtitle => _loc.Get("OperatorEditSubtitle");
    public string ResetDurationsText => _loc.Get("OperatorResetDurations");
    public string DeleteText => _loc.Get("DeleteProfile");
    public string NameWatermark => _loc.Get("RfidNameWatermark");
    public string SpaceLabel => _loc.Get("KeyboardSpace");
    public string DoneLabel => _loc.Get("KeyboardDone");
    public string CardIdDisplay => HasScannedCard
        ? string.Format(_loc.CurrentCulture, _loc.Get("RfidCardIdLabel"), ScannedCardId)
        : _loc.Get("RfidNoCardBadge");
    public string ReaderStatusText => _loc.Get(
        _rfidReader.IsHardwareReaderConnected ? "RfidReaderConnected" : "RfidReaderKeyboardFallback");

    public RfidUsersViewModel(
        RfidUserService userService,
        BrewProfileService profileService,
        IRfidReaderService rfidReader,
        INavigationService navigation,
        ILocalizationService loc,
        ILogger<RfidUsersViewModel> logger)
    {
        _userService = userService;
        _profileService = profileService;
        _rfidReader = rfidReader;
        _navigation = navigation;
        _loc = loc;
        _logger = logger;

        _rfidReader.CardScanned += OnCardScanned;
        _loc.LanguageChanged += OnLanguageChanged;

        _ = LoadUsersAsync();
    }

    public void StartRegistrationWithCard(string cardId)
    {
        var normalized = RfidUserService.NormalizeCardId(cardId);
        if (normalized is null) return;

        StatusMessage = string.Empty;
        ScannedCardId = normalized;
        Step = StepMode.EnteringName;

        var existing = Users.FirstOrDefault(u => string.Equals(u.CardId, normalized, StringComparison.OrdinalIgnoreCase));
        if (_editingUserId is null)
        {
            if (existing is not null)
            {
                UserNameInput = existing.Name;
                ExistingCardNotice = string.Format(_loc.CurrentCulture, _loc.Get("RfidExistingCardNotice"), existing.Name);
            }
            else
            {
                UserNameInput = string.Empty;
                ExistingCardNotice = string.Empty;
                _ = PopulateExistingCardNoticeAsync(normalized, allowPrefillName: true);
            }
        }
        else
        {
            if (existing is not null && existing.Id != _editingUserId.Value)
            {
                ExistingCardNotice = string.Format(_loc.CurrentCulture, _loc.Get("RfidExistingCardNotice"), existing.Name);
            }
            else
            {
                ExistingCardNotice = string.Empty;
                _ = PopulateExistingCardNoticeAsync(normalized, allowPrefillName: false);
            }
        }
    }

    public void StartCardlessRegistration() => BeginAddWithoutCard();

    private async Task PopulateExistingCardNoticeAsync(string normalizedCardId, bool allowPrefillName)
    {
        try
        {
            var existing = await _userService.FindByCardIdAsync(normalizedCardId, _disposeCts.Token);
            if (_disposed || !IsEnteringName || !string.Equals(ScannedCardId, normalizedCardId, StringComparison.Ordinal))
                return;

            if (existing is not null && existing.Id != _editingUserId)
            {
                if (allowPrefillName && string.IsNullOrEmpty(UserNameInput))
                    UserNameInput = existing.Name;
                ExistingCardNotice = string.Format(_loc.CurrentCulture, _loc.Get("RfidExistingCardNotice"), existing.Name);
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task LoadUsersAsync()
    {
        try
        {
            var users = await _userService.GetAllAsync(_disposeCts.Token);
            if (_disposed) return;

            Users.Clear();
            foreach (var u in users)
                Users.Add(u);
            HasUsers = Users.Count > 0;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load RFID users.");
            StatusMessage = string.Format(_loc.CurrentCulture, _loc.Get("ErrorPrefix"), ex.Message);
        }
    }

    private void OnCardScanned(object? sender, RfidCardScannedEvent e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_disposed || !IsWaitingForCard) return;
            StartRegistrationWithCard(e.CardId);
        });
    }

    private void OnLanguageChanged(string _)
    {
        foreach (var prop in new[]
        {
            nameof(Title), nameof(BackText), nameof(AddUserText), nameof(AddCardlessUserText),
            nameof(NoCardBadgeText), nameof(EmptyText), nameof(ScanCardTitle), nameof(ScanCardHint),
            nameof(ContinueWithoutCardText), nameof(CancelText), nameof(CardActionText),
            nameof(RemoveCardText), nameof(SaveText), nameof(EditText), nameof(EditDurationsText),
            nameof(EditDurationsSubtitle), nameof(ResetDurationsText), nameof(DeleteText),
            nameof(NameWatermark), nameof(SpaceLabel), nameof(DoneLabel), nameof(CardIdDisplay),
            nameof(ReaderStatusText)
        })
        {
            OnPropertyChanged(prop);
        }

        foreach (var item in EditingDurations)
            item.RefreshLocalization(_loc);
    }

    [RelayCommand]
    private void BackToSettings() => _navigation.NavigateTo<SettingsViewModel>();

    [RelayCommand]
    private void BeginAdd()
    {
        _editSessionGeneration++;
        IsBusy = false;
        _editingUserId = null;
        StatusMessage = string.Empty;
        ScannedCardId = string.Empty;
        UserNameInput = string.Empty;
        ExistingCardNotice = string.Empty;
        Step = StepMode.WaitingForCard;
    }

    [RelayCommand]
    private void BeginAddWithoutCard()
    {
        _editSessionGeneration++;
        IsBusy = false;
        _editingUserId = null;
        StatusMessage = string.Empty;
        ScannedCardId = string.Empty;
        UserNameInput = string.Empty;
        ExistingCardNotice = string.Empty;
        Step = StepMode.EnteringName;
    }

    [RelayCommand]
    private void ContinueWithoutCard()
    {
        ScannedCardId = string.Empty;
        ExistingCardNotice = string.Empty;
        Step = StepMode.EnteringName;
    }

    [RelayCommand]
    private void ScanOrChangeCard()
    {
        StatusMessage = string.Empty;
        ExistingCardNotice = string.Empty;
        Step = StepMode.WaitingForCard;
    }

    [RelayCommand]
    private void RemoveCard()
    {
        ScannedCardId = string.Empty;
        ExistingCardNotice = string.Empty;
    }

    [RelayCommand]
    private void EditUser(RfidUser user)
    {
        if (user is null) return;
        _editSessionGeneration++;
        IsBusy = false;
        _editingUserId = user.Id;
        StatusMessage = string.Empty;
        ScannedCardId = user.CardId ?? string.Empty;
        UserNameInput = user.Name;
        ExistingCardNotice = string.Empty;
        Step = StepMode.EnteringName;
    }

    [RelayCommand]
    private void CancelRegistration()
    {
        _editSessionGeneration++;
        IsBusy = false;
        _editingUserId = null;
        ScannedCardId = string.Empty;
        UserNameInput = string.Empty;
        ExistingCardNotice = string.Empty;
        EditingDurations.Clear();
        EditingDurationsTitle = string.Empty;
        Step = StepMode.List;
    }

    [RelayCommand]
    private async Task EditUserDurationsAsync(RfidUser user)
    {
        if (user is null) return;

        var generation = ++_editSessionGeneration;
        IsBusy = true;
        StatusMessage = string.Empty;
        try
        {
            var profiles = await _profileService.GetAllProfilesAsync(_disposeCts.Token);
            if (_disposed || generation != _editSessionGeneration) return;

            _editingUserId = user.Id;
            EditingDurationsTitle = string.Format(_loc.CurrentCulture, _loc.Get("OperatorEditTitle"), user.Name);
            EditingDurations.Clear();
            foreach (var profile in profiles)
            {
                var defaultMinutes = Math.Max(
                    RfidUserService.MinBrewDurationMinutes,
                    (int)Math.Round(profile.BrewDuration.TotalMinutes));
                var initialMinutes = Math.Clamp(
                    (int)Math.Round(RfidUserService.ResolveDuration(user, profile).TotalMinutes),
                    RfidUserService.MinBrewDurationMinutes,
                    RfidUserService.MaxBrewDurationMinutes);

                EditingDurations.Add(new OperatorProfileDurationItem(
                    profile.Id,
                    profile.Name,
                    profile.Icon,
                    defaultMinutes,
                    initialMinutes,
                    _loc,
                    hasCustomOverride: RfidUserService.HasCustomDuration(user, profile)));
            }
            Step = StepMode.EditingDurations;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load brew profiles for operator duration editor.");
            if (!_disposed && generation == _editSessionGeneration)
                StatusMessage = string.Format(_loc.CurrentCulture, _loc.Get("ErrorPrefix"), ex.Message);
        }
        finally
        {
            if (!_disposed && generation == _editSessionGeneration)
                IsBusy = false;
        }
    }

    [RelayCommand]
    private void ResetUserDurations()
    {
        foreach (var item in EditingDurations)
            item.ResetToDefault();
    }

    [RelayCommand]
    private async Task SaveUserDurationsAsync()
    {
        if (_editingUserId is null) return;

        var generation = _editSessionGeneration;
        var targetUserId = _editingUserId.Value;

        var overrides = new Dictionary<Guid, int>();
        foreach (var item in EditingDurations)
        {
            if (item.IsCustomized)
                overrides[item.ProfileId] = item.Minutes;
        }

        IsBusy = true;
        StatusMessage = string.Empty;
        try
        {
            var saved = await _userService.SaveProfilePreferencesAsync(targetUserId, overrides, _disposeCts.Token);
            if (_disposed || generation != _editSessionGeneration) return;

            await LoadUsersAsync();
            if (_disposed || generation != _editSessionGeneration) return;

            StatusMessage = string.Format(_loc.CurrentCulture, _loc.Get("OperatorDurationsSaved"), saved.Name);
            _editingUserId = null;
            EditingDurations.Clear();
            EditingDurationsTitle = string.Empty;
            Step = StepMode.List;
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save durations for operator {UserId}.", targetUserId);
            if (!_disposed && generation == _editSessionGeneration)
                StatusMessage = string.Format(_loc.CurrentCulture, _loc.Get("ErrorPrefix"), ex.Message);
        }
        finally
        {
            if (!_disposed && generation == _editSessionGeneration)
                IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveUserAsync()
    {
        var name = UserNameInput?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(name)) return;

        var cardId = RfidUserService.NormalizeCardId(ScannedCardId);
        IsBusy = true;
        try
        {
            var saved = await _userService.SaveUserAsync(name, cardId, _editingUserId, _disposeCts.Token);
            await LoadUsersAsync();
            if (_disposed) return;

            StatusMessage = string.Format(_loc.CurrentCulture, _loc.Get("RfidUserSaved"), saved.Name);
            _editingUserId = null;
            ScannedCardId = string.Empty;
            UserNameInput = string.Empty;
            ExistingCardNotice = string.Empty;
            Step = StepMode.List;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save operator {Name}.", name);
            StatusMessage = string.Format(_loc.CurrentCulture, _loc.Get("ErrorPrefix"), ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeleteUserAsync(RfidUser user)
    {
        if (user is null) return;
        IsBusy = true;
        try
        {
            await _userService.DeleteAsync(user.Id, _disposeCts.Token);
            await LoadUsersAsync();
            if (_disposed) return;
            StatusMessage = string.Format(_loc.CurrentCulture, _loc.Get("RfidUserDeleted"), user.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete operator {UserId}.", user.Id);
            StatusMessage = string.Format(_loc.CurrentCulture, _loc.Get("ErrorPrefix"), ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _rfidReader.CardScanned -= OnCardScanned;
        _loc.LanguageChanged -= OnLanguageChanged;
        _disposeCts.Cancel();
        _disposeCts.Dispose();
    }
}
