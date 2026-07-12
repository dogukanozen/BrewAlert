namespace BrewAlert.TeamsBot;

using System.Text.Json;
using Microsoft.Extensions.Options;

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

    public async Task AddAsync(string conversationId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(conversationId))
            throw new ArgumentException("Conversation ID is required.", nameof(conversationId));

        await gate.WaitAsync(cancellationToken);
        try
        {
            var ids = await ReadUnsafeAsync(cancellationToken);
            if (!ids.Add(conversationId))
                return;

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
            await File.WriteAllTextAsync(
                temporaryPath,
                JsonSerializer.Serialize(ids.Order(StringComparer.Ordinal), JsonOptions),
                cancellationToken);
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            gate.Release();
        }
    }

    public async Task<IReadOnlyCollection<string>> GetAllAsync(CancellationToken cancellationToken = default)
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

    private async Task<HashSet<string>> ReadUnsafeAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
            return new HashSet<string>(StringComparer.Ordinal);

        var json = await File.ReadAllTextAsync(path, cancellationToken);
        var values = JsonSerializer.Deserialize<string[]>(json) ?? [];
        return new HashSet<string>(values.Where(value => !string.IsNullOrWhiteSpace(value)), StringComparer.Ordinal);
    }
}
