using Avalonia.Headless.XUnit;
using BrewAlert.Core.Models;
using BrewAlert.Core.Services;
using BrewAlert.Core.Interfaces;
using BrewAlert.UI.Services;
using BrewAlert.UI.ViewModels;
using NSubstitute;
using Xunit;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace BrewAlert.UI.Tests;

public class ProfileListViewModelTests
{
    private readonly BrewProfileService _profileService;
    private readonly IProfileRepository _repository = Substitute.For<IProfileRepository>();
    private readonly INavigationService _navigation = Substitute.For<INavigationService>();

    public ProfileListViewModelTests()
    {
        _profileService = new BrewProfileService(_repository);
    }

    [AvaloniaFact]
    public async Task LoadProfiles_PopulatesCollection()
    {
        // Arrange
        var profiles = new List<BrewProfile>
        {
            new() { Name = "Tea" },
            new() { Name = "Coffee" }
        };
        _repository.GetAllAsync().Returns(profiles);

        // Act
        var vm = new ProfileListViewModel(_profileService, _navigation);
        
        // Give it a moment as it loads in constructor
        await Task.Delay(100);

        // Assert
        Assert.Equal(2, vm.Profiles.Count);
        Assert.Equal("Tea", vm.Profiles[0].Name);
        Assert.Equal("Coffee", vm.Profiles[1].Name);
    }

    [AvaloniaFact]
    public void SelectProfile_NavigatesAndStartsBrew()
    {
        // Arrange
        var profile = new BrewProfile { Name = "Tea" };
        var timerService = Substitute.For<IBrewTimerService>();
        timerService.Start(Arg.Any<BrewProfile>()).Returns(callInfo => new BrewSession { Profile = (BrewProfile)callInfo[0] });

        var timerVm = Substitute.For<BrewTimerViewModel>(
            timerService,
            Substitute.For<INotificationService>(),
            _navigation);
            
        var vm = new ProfileListViewModel(_profileService, _navigation);
        
        _navigation.CurrentView.Returns(vm, timerVm);

        // Act
        vm.SelectProfileCommand.Execute(profile);

        // Assert
        _navigation.Received(1).NavigateTo<BrewTimerViewModel>();
        timerVm.Received(1).StartBrew(profile);
    }
}
