using System;
using BrewAlert.Core.Models;

namespace BrewAlert.UI.ViewModels;

/// <summary>
/// Display projection of a <see cref="BrewProfile"/> resolved for a specific operator.
/// </summary>
public sealed record OperatorProfileChoiceItem(
    BrewProfile SourceProfile,
    BrewProfile EffectiveProfile,
    string Name,
    string Icon,
    string Description,
    TimeSpan EffectiveDuration,
    string DurationDisplay,
    bool IsCustomDuration,
    string BadgeText,
    string DefaultHintText);
