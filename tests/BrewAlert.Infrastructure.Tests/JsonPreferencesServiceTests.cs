using System.Reflection;
using BrewAlert.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace BrewAlert.Infrastructure.Tests;

public class JsonPreferencesServiceTests : IDisposable
{
    private readonly string _tempPath;
    private readonly JsonPreferencesService _service;

    public JsonPreferencesServiceTests()
    {
        _tempPath = Path.Combine(Path.GetTempPath(), $"preferences_{Guid.NewGuid()}.json");
        var logger = Substitute.For<ILogger<JsonPreferencesService>>();
        _service = new JsonPreferencesService(logger, _tempPath);
    }

    public void Dispose()
    {
        if (File.Exists(_tempPath)) File.Delete(_tempPath);
        var backupPath = _tempPath + ".bak";
        if (File.Exists(backupPath)) File.Delete(backupPath);
    }

    [Fact]
    public async Task GetNotificationProviderAsync_ReturnsNull_WhenFileDoesNotExist()
    {
        var result = await _service.GetNotificationProviderAsync();
        Assert.Null(result);
    }

    [Fact]
    public async Task SaveNotificationProviderAsync_CreatesFileAndSavesValue()
    {
        await _service.SaveNotificationProviderAsync("Webhook");

        Assert.True(File.Exists(_tempPath));
        var content = await File.ReadAllTextAsync(_tempPath);
        Assert.Contains("\"Provider\": \"Webhook\"", content);
    }

    [Fact]
    public async Task GetNotificationProviderAsync_ReadsSavedValue()
    {
        await _service.SaveNotificationProviderAsync("Graph");

        var result = await _service.GetNotificationProviderAsync();
        Assert.Equal("Graph", result);
    }

    [Fact]
    public async Task SaveNotificationProviderAsync_OverwritesExistingValue()
    {
        await _service.SaveNotificationProviderAsync("Graph");
        await _service.SaveNotificationProviderAsync("Webhook");

        var result = await _service.GetNotificationProviderAsync();
        Assert.Equal("Webhook", result);
    }

    [Fact]
    public async Task GetNotificationProviderAsync_CreatesBackup_WhenFileIsCorrupted()
    {
        await File.WriteAllTextAsync(_tempPath, "{ invalid json ]");

        var result = await _service.GetNotificationProviderAsync();

        Assert.Null(result);
        Assert.True(File.Exists(_tempPath + ".bak"));
    }
}
