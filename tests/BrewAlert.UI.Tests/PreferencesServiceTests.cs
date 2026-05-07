using BrewAlert.UI.Services;
using Xunit;

namespace BrewAlert.UI.Tests;

public sealed class PreferencesServiceTests : IDisposable
{
    private readonly string _filePath;
    private readonly PreferencesService _sut;

    public PreferencesServiceTests()
    {
        _filePath = Path.Combine(Path.GetTempPath(), $"brewalert_prefs_test_{Guid.NewGuid()}.json");
        _sut = new PreferencesService(_filePath);
    }

    public void Dispose()
    {
        if (File.Exists(_filePath))
            File.Delete(_filePath);
    }

    [Fact]
    public async Task SaveNotificationProvider_PersistsAndRoundTrips()
    {
        await _sut.SaveNotificationProviderAsync("Webhook");

        var json = await File.ReadAllTextAsync(_filePath);
        Assert.Contains("Webhook", json);
    }

    [Fact]
    public async Task SaveLanguage_PersistsAndRoundTrips()
    {
        await _sut.SaveLanguageAsync("Turkish");

        var json = await File.ReadAllTextAsync(_filePath);
        Assert.Contains("Turkish", json);
    }

    [Fact]
    public async Task SaveWebhookUrl_PersistsUrl()
    {
        await _sut.SaveWebhookUrlAsync("https://prod.webhook.office.com/abc123");

        var json = await File.ReadAllTextAsync(_filePath);
        Assert.Contains("https://prod.webhook.office.com/abc123", json);
    }

    [Fact]
    public async Task SaveWebhookUrl_TrimsWhitespace()
    {
        await _sut.SaveWebhookUrlAsync("  https://prod.webhook.office.com/abc  ");

        var json = await File.ReadAllTextAsync(_filePath);
        Assert.Contains("https://prod.webhook.office.com/abc", json);
        Assert.DoesNotContain("  https", json);
    }

    [Fact]
    public async Task SaveWebhookUrl_DoesNotClobberExistingPreferences()
    {
        await _sut.SaveLanguageAsync("Turkish");
        await _sut.SaveNotificationProviderAsync("Webhook");
        await _sut.SaveWebhookUrlAsync("https://example.com/hook");

        var json = await File.ReadAllTextAsync(_filePath);
        Assert.Contains("Turkish", json);
        Assert.Contains("Webhook", json);
        Assert.Contains("https://example.com/hook", json);
    }

    [Fact]
    public async Task SaveWebhookUrl_OverwritesPreviousUrl()
    {
        await _sut.SaveWebhookUrlAsync("https://old.example.com/hook");
        await _sut.SaveWebhookUrlAsync("https://new.example.com/hook");

        var json = await File.ReadAllTextAsync(_filePath);
        Assert.Contains("https://new.example.com/hook", json);
        Assert.DoesNotContain("https://old.example.com/hook", json);
    }
}
