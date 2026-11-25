namespace BrewAlert.Core.Models;

/// <summary>
/// Represents a registered operator who can initiate brews, optionally linked to an RFID card.
/// </summary>
public sealed class RfidUser
{
    private Dictionary<Guid, int> _profileDurationMinutes = [];

    public Guid Id { get; init; } = Guid.NewGuid();
    public string? CardId { get; set; }
    public required string Name { get; set; }
    public bool HasConfiguredProfiles { get; set; }

    public Dictionary<Guid, int> ProfileDurationMinutes
    {
        get => _profileDurationMinutes;
        set => _profileDurationMinutes = value ?? [];
    }

    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
}
