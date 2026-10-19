using System.Runtime.CompilerServices;
using BrewAlert.UI.Services;
using BrewAlert.UI.ViewModels;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;
using System;

namespace BrewAlert.UI.Tests;

public class TestViewModel : ViewModelBase { }

public sealed class DisposableTestViewModel : ViewModelBase, IDisposable
{
    public bool IsDisposed { get; private set; }
    public void Dispose() => IsDisposed = true;
}

public class NavigationServiceTests
{
    [Fact]
    public void NavigateTo_Generic_ResolvesFromServiceProviderAndNotifies()
    {
        // Arrange
        using var provider = new ServiceCollection().AddTransient<TestViewModel>().BuildServiceProvider();
        using var navigation = new NavigationService(provider);
        ViewModelBase? notifiedVm = null;
        navigation.CurrentViewChanged += vm => notifiedVm = vm;

        // Act
        navigation.NavigateTo<TestViewModel>();

        // Assert
        Assert.IsType<TestViewModel>(navigation.CurrentView);
        Assert.Same(navigation.CurrentView, notifiedVm);
    }

    [Fact]
    public void NavigateTo_Generic_DisposesPreviousViewModel()
    {
        using var provider = new ServiceCollection().AddTransient<DisposableTestViewModel>().BuildServiceProvider();
        using var navigation = new NavigationService(provider);
        navigation.NavigateTo<DisposableTestViewModel>();
        var first = (DisposableTestViewModel)navigation.CurrentView;

        navigation.NavigateTo<DisposableTestViewModel>();

        Assert.True(first.IsDisposed);
        Assert.False(((DisposableTestViewModel)navigation.CurrentView).IsDisposed);
    }

    [Fact]
    public void NavigateTo_Generic_DoesNotRetainPreviousViewModels()
    {
        using var provider = new ServiceCollection().AddTransient<DisposableTestViewModel>().BuildServiceProvider();
        using var navigation = new NavigationService(provider);

        var previous = NavigateRepeatedly(navigation, 50);
        navigation.NavigateTo<DisposableTestViewModel>();
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.All(previous, reference => Assert.False(reference.IsAlive));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static List<WeakReference> NavigateRepeatedly(NavigationService navigation, int count)
    {
        var references = new List<WeakReference>();
        for (var i = 0; i < count; i++)
        {
            navigation.NavigateTo<DisposableTestViewModel>();
            references.Add(new WeakReference(navigation.CurrentView));
        }
        return references;
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
