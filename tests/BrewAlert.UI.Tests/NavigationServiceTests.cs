using BrewAlert.Core.Interfaces;
using BrewAlert.Core.Services;
using BrewAlert.UI.Services;
using BrewAlert.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;
using System;

namespace BrewAlert.UI.Tests;

public class NavigationServiceTests
{
    [Fact]
    public void NavigateTo_Generic_ResolvesFromServiceProviderAndNotifies()
    {
        // Arrange
        var serviceProvider = Substitute.For<IServiceProvider>();
        var repository = Substitute.For<IProfileRepository>();
        var profileService = new BrewProfileService(repository);
        var mockVm = Substitute.For<ProfileListViewModel>(
            profileService,
            Substitute.For<INavigationService>());
        serviceProvider.GetService(typeof(ProfileListViewModel)).Returns(mockVm);
        
        var navigation = new NavigationService(serviceProvider);
        ViewModelBase? notifiedVm = null;
        navigation.CurrentViewChanged += vm => notifiedVm = vm;

        // Act
        navigation.NavigateTo<ProfileListViewModel>();

        // Assert
        Assert.Equal(mockVm, navigation.CurrentView);
        Assert.Equal(mockVm, notifiedVm);
    }

    [Fact]
    public void NavigateTo_Instance_SetsCurrentViewAndNotifies()
    {
        // Arrange
        var serviceProvider = Substitute.For<IServiceProvider>();
        var navigation = new NavigationService(serviceProvider);
        var mockVm = Substitute.For<ViewModelBase>();
        ViewModelBase? notifiedVm = null;
        navigation.CurrentViewChanged += vm => notifiedVm = vm;

        // Act
        navigation.NavigateTo(mockVm);

        // Assert
        Assert.Equal(mockVm, navigation.CurrentView);
        Assert.Equal(mockVm, notifiedVm);
    }
}
