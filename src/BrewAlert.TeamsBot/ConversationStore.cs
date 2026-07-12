namespace BrewAlert.TeamsBot;

using System.Text.Json;
using Microsoft.Extensions.Options;

public sealed record ConversationTarget(string Id, string ServiceUrl, string ConversationType);

public sealed class ConversationStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly string path;

    public ConversationStore(IOptions<BrewAlertBotOptions> options, IHostEnvironment environment)
    {
        var configuredPath = options.Value.ConversationStorePath;
        path = Path.IsPathRooted(configuredPath)
            ? configuredPath
            : Path.Combine(environment.ContentRootPath, configuredPath);
    }

    public Task AddAsync(string conversationId, CancellationToken cancellationToken = default) =>
        AddAsync(new ConversationTarget(conversationId, string.Empty, string.Empty), cancellationToken);

    public async Task AddAsync(ConversationTarget target, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(target.Id))
            throw new ArgumentException("Conversation ID is required.", nameof(target));

        await gate.WaitAsync(cancellationToken);
        try
        {
            var targets = await ReadUnsafeAsync(cancellationToken);
            var existing = targets.FirstOrDefault(value => string.Equals(value.Id, target.Id, StringComparison.Ordinal));
            var merged = new ConversationTarget(
                target.Id,
                FirstNotEmpty(target.ServiceUrl, existing?.ServiceUrl),
                FirstNotEmpty(target.ConversationType, existing?.ConversationType));

            if (existing == merged)
                return;

            targets.RemoveAll(value => string.Equals(value.Id, target.Id, StringComparison.Ordinal));
            targets.Add(merged);

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
            await File.WriteAllTextAsync(
                temporaryPath,
                JsonSerializer.Serialize(targets.OrderBy(value => value.Id, StringComparer.Ordinal), JsonOptions),
                cancellationToken);
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<IReadOnlyCollection<ConversationTarget>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            return (await ReadUnsafeAsync(cancellationToken)).ToArray();
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<List<ConversationTarget>> ReadUnsafeAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
            return [];

        var json = await File.ReadAllTextAsync(path, cancellationToken);
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
            return [];

        return document.RootElement.EnumerateArray()
            .Select(element => element.ValueKind == JsonValueKind.String
                ? new ConversationTarget(element.GetString() ?? string.Empty, string.Empty, string.Empty)
                : element.Deserialize<ConversationTarget>(JsonOptions))
            .Where(target => target is not null && !string.IsNullOrWhiteSpace(target.Id))
            .Cast<ConversationTarget>()
            .ToList();
    }

    private static string FirstNotEmpty(string? preferred, string? fallback) =>
        !string.IsNullOrWhiteSpace(preferred) ? preferred : fallback ?? string.Empty;
}
