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
    private int _minutes;

    public bool IsCustomized => Minutes != DefaultMinutes;

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
        Action? onModified = null)
    {
        ProfileId = profileId;
        Name = name;
        Icon = icon;
        DefaultMinutes = Math.Clamp(defaultMinutes, RfidUserService.MinBrewDurationMinutes, RfidUserService.MaxBrewDurationMinutes);
        _loc = loc;
        _onModified = onModified;
        _minutes = Math.Clamp(initialMinutes, RfidUserService.MinBrewDurationMinutes, RfidUserService.MaxBrewDurationMinutes);
    }

    partial void OnMinutesChanged(int value)
    {
        OnPropertyChanged(nameof(IsCustomized));
        OnPropertyChanged(nameof(DurationText));
        _onModified?.Invoke();
    }

    [RelayCommand]
    private void Increase()
    {
        if (Minutes < RfidUserService.MaxBrewDurationMinutes)
            Minutes++;
    }

    [RelayCommand]
    private void Decrease()
    {
        if (Minutes > RfidUserService.MinBrewDurationMinutes)
            Minutes--;
    }

    public void ResetToDefault() => Minutes = DefaultMinutes;

    public void RefreshLocalization(ILocalizationService loc)
    {
        _loc = loc;
        OnPropertyChanged(nameof(DurationText));
        OnPropertyChanged(nameof(DefaultHintText));
    }
}
