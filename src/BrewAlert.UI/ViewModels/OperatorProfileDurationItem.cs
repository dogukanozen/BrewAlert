using System;
using BrewAlert.Core.Services;
using BrewAlert.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace BrewAlert.UI.ViewModels;

/// <summary>
/// Editable per-operator brew duration row for a single global <see cref="Core.Models.BrewProfile"/>.
/// </summary>
public partial class OperatorProfileDurationItem : ViewModelBase
{
    private ILocalizationService _loc;
    private readonly Action? _onModified;

    public Guid ProfileId { get; }
    public string Name { get; }
    public string Icon { get; }
    public int DefaultMinutes { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCustomized))]
    private int _minutes;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCustomized))]
    private bool _hasCustomOverride;

    public bool IsCustomized => HasCustomOverride && Minutes != DefaultMinutes;

    public string DurationText => $"{Minutes} {_loc.Get("MinShort")}";

    public string DefaultHintText =>
        string.Format(_loc.CurrentCulture, _loc.Get("OperatorDefaultDurationHint"), DefaultMinutes);

    public OperatorProfileDurationItem(
        Guid profileId,
        string name,
        string icon,
        int defaultMinutes,
        int initialMinutes,
        ILocalizationService loc,
        Action? onModified = null,
        bool hasCustomOverride = false)
    {
        ProfileId = profileId;
        Name = name;
        Icon = icon;
        DefaultMinutes = Math.Max(RfidUserService.MinBrewDurationMinutes, defaultMinutes);
        _loc = loc;
        _onModified = onModified;
        _minutes = Math.Clamp(initialMinutes, RfidUserService.MinBrewDurationMinutes, RfidUserService.MaxBrewDurationMinutes);
        _hasCustomOverride = hasCustomOverride && _minutes != DefaultMinutes;
    }

    partial void OnMinutesChanged(int value)
    {
        OnPropertyChanged(nameof(DurationText));
        _onModified?.Invoke();
    }

    [RelayCommand]
    private void Increase()
    {
        if (Minutes < RfidUserService.MaxBrewDurationMinutes)
        {
            Minutes++;
            HasCustomOverride = Minutes != DefaultMinutes;
        }
    }

    [RelayCommand]
    private void Decrease()
    {
        if (Minutes > RfidUserService.MinBrewDurationMinutes)
        {
            Minutes--;
            HasCustomOverride = Minutes != DefaultMinutes;
        }
    }

    public void ResetToDefault()
    {
        HasCustomOverride = false;
        Minutes = Math.Clamp(DefaultMinutes, RfidUserService.MinBrewDurationMinutes, RfidUserService.MaxBrewDurationMinutes);
    }

    public void RefreshLocalization(ILocalizationService loc)
    {
        _loc = loc;
        OnPropertyChanged(nameof(DurationText));
        OnPropertyChanged(nameof(DefaultHintText));
    }
}
