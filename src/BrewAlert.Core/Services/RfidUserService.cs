namespace BrewAlert.Core.Services;

using BrewAlert.Core.Interfaces;
using BrewAlert.Core.Models;

/// <summary>
/// Domain service for managing operators and looking up brewers by optional RFID card ID.
/// </summary>
public sealed class RfidUserService(IRfidUserRepository repository, TimeProvider? timeProvider = null)
{
    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;

    public static string? NormalizeCardId(string? cardId) =>
        string.IsNullOrWhiteSpace(cardId) ? null : cardId.Trim().ToUpperInvariant();

    public async Task<IReadOnlyList<RfidUser>> GetAllAsync(CancellationToken ct = default)
    {
        var users = await repository.GetAllAsync(ct);
        return users.OrderBy(u => u.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public async Task<RfidUser?> FindByCardIdAsync(string? cardId, CancellationToken ct = default)
    {
        var normalized = NormalizeCardId(cardId);
        if (normalized is null) return null;
        return await repository.GetByCardIdAsync(normalized, ct);
    }

    public Task<RfidUser> RegisterAsync(string? cardId, string name, CancellationToken ct = default) =>
        SaveUserAsync(name, cardId, existingUserId: null, ct);

    public async Task<RfidUser> SaveUserAsync(
        string name,
        string? cardId = null,
        Guid? existingUserId = null,
        CancellationToken ct = default)
    {
        var trimmedName = name?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(trimmedName))
            throw new ArgumentException("User name cannot be empty.", nameof(name));

        var normalizedCardId = NormalizeCardId(cardId);

        if (existingUserId is not null)
        {
            var users = await repository.GetAllAsync(ct);
            var byId = users.FirstOrDefault(u => u.Id == existingUserId.Value);
            if (byId is not null)
            {
                byId.Name = trimmedName;
                byId.CardId = normalizedCardId;
                await repository.SaveAsync(byId, ct);
                return byId;
            }
        }

        if (normalizedCardId is not null)
        {
            var existingByCard = await repository.GetByCardIdAsync(normalizedCardId, ct);
            if (existingByCard is not null)
            {
                existingByCard.CardId = normalizedCardId;
                existingByCard.Name = trimmedName;
                await repository.SaveAsync(existingByCard, ct);
                return existingByCard;
            }
        }

        var user = new RfidUser
        {
            Id = existingUserId ?? Guid.NewGuid(),
            CardId = normalizedCardId,
            Name = trimmedName,
            CreatedAtUtc = _time.GetUtcNow().UtcDateTime,
        };
        await repository.SaveAsync(user, ct);
        return user;
    }

    public Task DeleteAsync(Guid id, CancellationToken ct = default) => repository.DeleteAsync(id, ct);
}
