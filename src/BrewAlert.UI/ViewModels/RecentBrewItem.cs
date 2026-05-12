using BrewAlert.Core.Models;

namespace BrewAlert.UI.ViewModels;

/// <summary>UI projection of a <see cref="BrewHistoryEntry"/> with pre-formatted display text.</summary>
public sealed record RecentBrewItem(string Icon, string Name, string RelativeTime, string DurationText);
