namespace BrewAlert.Core.Models;

/// <summary>
/// Represents a reusable brew configuration (e.g., "Turkish Tea — 8 min").
/// </summary>
public sealed class BrewProfile
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Name { get; set; }
    public BrewType Type { get; set; }
    public TimeSpan BrewDuration { get; set; }
    public string Icon { get; set; } = "☕";
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
}
