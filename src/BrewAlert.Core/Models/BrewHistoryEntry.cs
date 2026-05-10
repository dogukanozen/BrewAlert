namespace BrewAlert.Core.Models;

/// <summary>One persisted brew completion record.</summary>
public sealed record BrewHistoryEntry(
    Guid Id,
    DateTime CompletedAtUtc,
    string ProfileName,
    BrewType Type,
    string Icon,
    int DurationSeconds);
