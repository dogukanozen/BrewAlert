using BrewAlert.UI.Services;
using System.Text.Json.Nodes;
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

        var backupPath = _filePath + ".bak";
        if (File.Exists(backupPath))
            File.Delete(backupPath);
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

    [Fact]
    public async Task AppearanceAndOtherSettings_PreserveEachOtherAndUnknownFields()
    {
        await File.WriteAllTextAsync(_filePath, """
            {"FutureRoot":17,"BrewAlert":{"FutureOption":"keep","Notifications":{"Teams":{"ApiKey":"example-key"}}}}
            """);
        await _sut.SaveAppearanceAsync(new AppearanceOptions { Mode = "Weekly", Friday = "Porcelain" });
        await Task.WhenAll(_sut.SaveLanguageAsync("Turkish"), _sut.SaveNotificationProviderAsync("Webhook"),
            _sut.SaveWebhookUrlAsync("https://example.com/hook"));
        var json = JsonNode.Parse(await File.ReadAllTextAsync(_filePath))!;
        Assert.Equal(17, json["FutureRoot"]!.GetValue<int>());
        Assert.Equal("keep", json["BrewAlert"]!["FutureOption"]!.GetValue<string>());
        Assert.Equal("example-key", json["BrewAlert"]!["Notifications"]!["Teams"]!["ApiKey"]!.GetValue<string>());
        Assert.Equal("Porcelain", json["BrewAlert"]!["Appearance"]!["Friday"]!.GetValue<string>());
        Assert.Equal("Turkish", json["BrewAlert"]!["Language"]!.GetValue<string>());
        Assert.Equal("Webhook", json["BrewAlert"]!["Notifications"]!["Provider"]!.GetValue<string>());
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("""{"BrewAlert":42}""")]
    public async Task InvalidPreferenceShape_IsBackedUpAndRecovered(string invalid)
    {
        await File.WriteAllTextAsync(_filePath, invalid);
        await _sut.SaveAppearanceAsync(new AppearanceOptions { Mode = "Porcelain" });
        Assert.Equal(invalid, await File.ReadAllTextAsync(_filePath + ".bak"));
        Assert.Equal("Porcelain", JsonNode.Parse(await File.ReadAllTextAsync(_filePath))!["BrewAlert"]!["Appearance"]!["Mode"]!.GetValue<string>());
    }

    [Fact]
    public async Task Save_OnCorruptedFile_BacksUpAndRecovers()
    {
        await File.WriteAllTextAsync(_filePath, "{ this is not valid json");

        await _sut.SaveNotificationProviderAsync("Webhook");

        var json = await File.ReadAllTextAsync(_filePath);
        Assert.Contains("Webhook", json);

        var backupPath = _filePath + ".bak";
        Assert.True(File.Exists(backupPath));
        Assert.Contains("this is not valid json", await File.ReadAllTextAsync(backupPath));
    }
}
