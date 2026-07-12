using BrewAlert.TeamsBot;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace BrewAlert.TeamsBot.Tests;

public sealed class ConversationStoreTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"brewalert-bot-{Guid.NewGuid():N}");

    [Fact]
    public async Task AddAsync_PersistsUniqueConversationIds()
    {
        var store = CreateStore();

        await store.AddAsync("conversation-1");
        await store.AddAsync("conversation-1");
        await store.AddAsync("conversation-2");

        var reloaded = CreateStore();
        var ids = await reloaded.GetAllAsync();
        Assert.Equal(["conversation-1", "conversation-2"], ids.Order(StringComparer.Ordinal));
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
            Directory.Delete(directory, recursive: true);
    }

    private ConversationStore CreateStore()
    {
        var options = Options.Create(new BrewAlertBotOptions
        {
            ConversationStorePath = Path.Combine(directory, "conversations.json"),
        });
        return new ConversationStore(options, new TestHostEnvironment(directory));
    }

    private sealed class TestHostEnvironment(string contentRootPath) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "BrewAlert.TeamsBot.Tests";
        public string ContentRootPath { get; set; } = contentRootPath;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
