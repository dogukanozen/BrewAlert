using Avalonia.Headless.XUnit;
using BrewAlert.Core.Models;
using BrewAlert.Core.Services;
using BrewAlert.Core.Interfaces;
using BrewAlert.UI.Services;
using BrewAlert.UI.ViewModels;
using NSubstitute;
using Xunit;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
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
            new() { Name = "Tea", BrewDuration = TimeSpan.FromMinutes(3) },
            new() { Name = "Coffee", BrewDuration = TimeSpan.FromMinutes(3) }
        };
        _repository.GetAllAsync(Arg.Any<CancellationToken>()).Returns(profiles);

        // Act
        var tcs = new TaskCompletionSource();
        var vm = new ProfileListViewModel(_profileService, _navigation);
        
        if (!vm.IsLoading)
        {
            tcs.SetResult();
        }
        else
        {
            vm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(vm.IsLoading) && !vm.IsLoading)
                {
                    tcs.TrySetResult();
                }
            };
        }

        // Wait for completion with timeout (avoiding flaky WhenAny(Delay))
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await tcs.Task.WaitAsync(cts.Token);

        // Assert
        Assert.Equal(2, vm.Profiles.Count);
        Assert.Equal("Tea", vm.Profiles[0].Name);
    }

    [AvaloniaFact]
    public void SelectProfile_NavigatesAndStartsBrew()
    {
        // Arrange
        var profile = new BrewProfile { Name = "Tea", BrewDuration = TimeSpan.FromMinutes(3) };
        var timerService = Substitute.For<IBrewTimerService>();
        timerService.Start(Arg.Any<BrewProfile>()).Returns(new BrewSession { Profile = profile });

        // Use real instance because StartBrew is not virtual
        var timerVm = new BrewTimerViewModel(
            timerService,
            Substitute.For<INotificationService>(),
            _navigation);
            
        var vm = new ProfileListViewModel(_profileService, _navigation);
        
        // Setup navigation to return vm then timerVm
        _navigation.CurrentView.Returns(vm, timerVm);

        // Act
        vm.SelectProfileCommand.Execute(profile);

        // Assert
        _navigation.Received(1).NavigateTo<BrewTimerViewModel>();
        timerService.Received(1).Start(profile);
    }
}
