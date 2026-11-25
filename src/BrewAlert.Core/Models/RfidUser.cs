namespace BrewAlert.Core.Models;

/// <summary>
/// Represents a registered operator who can initiate brews, optionally linked to an RFID card.
/// </summary>
public sealed class RfidUser
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string? CardId { get; set; }
    public required string Name { get; set; }
    public DateTime CreatedAtUtc { get; init; } = DateTime.UtcNow;
}
