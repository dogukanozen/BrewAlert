using BrewAlert.UI.Services;
using BrewAlert.UI.ViewModels;
using NSubstitute;
using Xunit;
using System;

namespace BrewAlert.UI.Tests;

public class TestViewModel : ViewModelBase { }

public class NavigationServiceTests
{
    [Fact]
    public void NavigateTo_Generic_ResolvesFromServiceProviderAndNotifies()
    {
        // Arrange
        var serviceProvider = Substitute.For<IServiceProvider>();
        var mockVm = new TestViewModel();
        serviceProvider.GetService(typeof(TestViewModel)).Returns(mockVm);

        var navigation = new NavigationService(serviceProvider);
        ViewModelBase? notifiedVm = null;
        navigation.CurrentViewChanged += vm => notifiedVm = vm;

        // Act
        navigation.NavigateTo<TestViewModel>();

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

    [Fact]
    public void NavigateTo_DisposesOldViewModelWhenDifferentInstanceIsSet()
    {
        // Arrange
        var serviceProvider = Substitute.For<IServiceProvider>();
        var navigation = new NavigationService(serviceProvider);
        var disposableVm = Substitute.For<ViewModelBase, IDisposable>();
        navigation.NavigateTo(disposableVm);

        var nextVm = Substitute.For<ViewModelBase>();

        // Act
        navigation.NavigateTo(nextVm);

        // Assert — previous IDisposable VM is disposed
        ((IDisposable)disposableVm).Received(1).Dispose();
        Assert.Equal(nextVm, navigation.CurrentView);
    }

    [Fact]
    public void NavigateTo_SameInstance_DoesNotDispose()
    {
        // Arrange
        var serviceProvider = Substitute.For<IServiceProvider>();
        var navigation = new NavigationService(serviceProvider);
        var vm = Substitute.For<ViewModelBase, IDisposable>();
        navigation.NavigateTo(vm);

        // Act — navigate to the same instance again
        navigation.NavigateTo(vm);

        // Assert — must not be disposed
        ((IDisposable)vm).DidNotReceive().Dispose();
    }
}
