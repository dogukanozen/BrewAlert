using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using BrewAlert.Core.Interfaces;
using BrewAlert.Core.Models;
using BrewAlert.UI.Services;
using BrewAlert.UI.ViewModels;
using BrewAlert.UI.Views;
using NSubstitute;
using Xunit;

namespace BrewAlert.UI.Tests;

public sealed class AmbientMotionTests
{
    [AvaloniaTheory]
    [InlineData(BrewTheme.Sumi)]
    [InlineData(BrewTheme.Ibre)]
    [InlineData(BrewTheme.Aizome)]
    public void ThemeBackdrop_StopsWhenHiddenInactiveOrDetached(BrewTheme appearance)
    {
        var backdrop = new ThemeBackdrop { Appearance = appearance, Stroke = Brushes.Gray, IsActive = true };
        Assert.False(backdrop.IsRunning);
        var window = new Window { Width = 800, Height = 480, Content = backdrop };
        try
        {
            window.Show();
            Assert.True(backdrop.IsRunning);
            backdrop.IsActive = false;
            Assert.False(backdrop.IsRunning);
            backdrop.IsActive = true;
            Assert.True(backdrop.IsRunning);
            backdrop.IsVisible = false;
            Assert.False(backdrop.IsRunning);
            backdrop.IsVisible = true;
            Assert.True(backdrop.IsRunning);
            window.Content = null;
            Assert.False(backdrop.IsRunning);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Silk_StopsWhenInactiveAndDetached_ResumesWhenShown()
    {
        var silk = new SilkBackdrop { Stroke = Brushes.Gray };
        var window = new Window { Width = 800, Height = 480, Content = silk };
        Assert.False(silk.IsRunning);
        silk.IsActive = true;
        Assert.False(silk.IsRunning);
        try
        {
            window.Show();
            Assert.True(silk.IsRunning);
            silk.IsActive = false;
            Assert.False(silk.IsRunning);
            silk.IsActive = true;
            Assert.True(silk.IsRunning);
            silk.IsVisible = false;
            Assert.False(silk.IsRunning);
            silk.IsVisible = true;
            Assert.True(silk.IsRunning);
            window.Content = null;
            Assert.False(silk.IsRunning);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Clock_ChangesOnlyTheMinuteDigit_AndCancelsWhenHiddenOrDetached()
    {
        var clock = new AnimatedClock { Text = "14:32", FontSize = 88, IsActive = true };
        var window = new Window { Width = 800, Height = 480, Content = clock };
        try
        {
            window.Show();
            Assert.True(clock.IsTransitionRunning);
            clock.Text = "14:33";
            Assert.Equal(1, clock.ChangedDigitCount);
            Assert.True(clock.IsTransitionRunning);
            clock.IsActive = false;
            Assert.False(clock.IsTransitionRunning);
            Assert.Equal("14:33", clock.Text);
            clock.IsActive = true;
            Assert.True(clock.IsTransitionRunning);
            window.Content = null;
            Assert.False(clock.IsTransitionRunning);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void SilkMode_GatesItsTimerAndKeepsOtherLayersOff()
    {
        var history = Substitute.For<IBrewHistoryService>();
        var loc = Substitute.For<ILocalizationService>();
        loc.CurrentCulture.Returns(System.Globalization.CultureInfo.GetCultureInfo("tr-TR"));
        loc.Get(Arg.Any<string>()).Returns(x => x.Arg<string>());
        using var vm = new AmbientOverlayViewModel(history, loc);
        vm.Mode = AmbientMode.Silk;
        Assert.True(vm.IsSilkMode);
        Assert.False(vm.IsSilkAnimationActive);
        var notifications = new List<string?>();
        vm.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);
        vm.IsVisible = true;
        Assert.True(vm.IsSilkAnimationActive);
        Assert.Contains(nameof(vm.IsSilkAnimationActive), notifications);
        Assert.False(vm.IsOrganicBackdrop || vm.IsCosmicBackdrop || vm.IsPrismaticBackdrop);
        Assert.Equal(0, vm.CardOpacity);
        notifications.Clear();
        vm.Mode = AmbientMode.Radar;
        Assert.False(vm.IsSilkAnimationActive);
        Assert.Contains(nameof(vm.IsSilkMode), notifications);
        Assert.Contains(nameof(vm.IsSilkAnimationActive), notifications);
    }
}
