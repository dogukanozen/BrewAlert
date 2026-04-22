using BrewAlert.Core.Interfaces;
using BrewAlert.Core.Models;
using BrewAlert.Core.Services;
using BrewAlert.Infrastructure.Configuration;
using BrewAlert.UI.ViewModels;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BrewAlert.UI.Tests;

public class SettingsViewModelTests
{
    private readonly INotificationService _notificationService = Substitute.For<INotificationService>();
    private readonly IProfileRepository _repository = Substitute.For<IProfileRepository>();
    private readonly BrewProfileService _profileService;

    public SettingsViewModelTests()
    {
        _profileService = new BrewProfileService(_repository);
    }

    [Fact]
    public void Constructor_MasksSensitiveInformation()
    {
        // Arrange
        var options = Options.Create(new TeamsGraphOptions
        {
            TenantId = "12345678-90ab-cdef-1234-567890abcdef",
            ClientId = "abcdef12-3456-7890-abcd-ef1234567890",
            ChatId = "19:abc@thread.v2",
            Enabled = true
        });

        // Act
        var vm = new SettingsViewModel(_notificationService, options, _profileService);

        // Assert
        Assert.StartsWith("12345678", vm.TenantId);
        Assert.Contains("••••••••", vm.TenantId);
        Assert.Equal("19:abc@thread.v2", vm.ChatId); // ChatId is not masked
    }

    [Fact]
    public async Task TestConnection_SetsResult()
    {
        // Arrange
        var options = Options.Create(new TeamsGraphOptions
        {
            TenantId = "T", ClientId = "C", ClientSecret = "S", ChatId = "CH", Enabled = true
        });
        _notificationService.TestConnectionAsync().Returns(true);
        var vm = new SettingsViewModel(_notificationService, options, _profileService);

        // Act
        await vm.TestConnectionCommand.ExecuteAsync(null);

        // Assert
        Assert.Contains("başarılı", vm.TestResult);
    }
}
