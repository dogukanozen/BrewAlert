namespace BrewAlert.Core.Interfaces;

using BrewAlert.Core.Models;

/// <summary>
/// Persistence abstraction for registered RFID users.
/// </summary>
public interface IRfidUserRepository
{
    Task<IReadOnlyList<RfidUser>> GetAllAsync(CancellationToken ct = default);
    Task<RfidUser?> GetByCardIdAsync(string cardId, CancellationToken ct = default);
    Task SaveAsync(RfidUser user, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
