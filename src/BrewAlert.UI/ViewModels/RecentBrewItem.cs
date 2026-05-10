using BrewAlert.Core.Models;

namespace BrewAlert.UI.ViewModels;

/// <summary>UI projection of a <see cref="BrewHistoryEntry"/> with a pre-formatted relative time.</summary>
public sealed record RecentBrewItem(string Icon, string Name, string RelativeTime);
