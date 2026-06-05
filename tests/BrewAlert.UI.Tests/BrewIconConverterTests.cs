using System.Globalization;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using BrewAlert.UI.Converters;
using BrewAlert.UI.Media;
using Xunit;

namespace BrewAlert.UI.Tests;

public class BrewIconConverterTests
{
    private static object? Convert(string? icon) =>
        BrewIconConverter.Instance.Convert(icon, typeof(Geometry), null, CultureInfo.InvariantCulture);

    // The stored profile/history icons in play across the app. Each must resolve to a vector
    // so the device screen never shows tofu for a known brew icon.
    [AvaloniaTheory]
    [InlineData("☕")]
    [InlineData("♨")]
    [InlineData("🍵")]
    [InlineData("🫖")]
    public void Convert_KnownEmoji_ReturnsGeometry(string icon)
    {
        Assert.IsAssignableFrom<Geometry>(Convert(icon));
    }

    [AvaloniaFact]
    public void Convert_UnknownEmoji_FallsBackToCup()
    {
        // A profile with an unexpected icon must still render a shape, not tofu.
        Assert.Same(BrewIconGeometry.Cup, Convert("🧋"));
    }

    [AvaloniaTheory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Convert_BlankIcon_ReturnsNull(string? icon)
    {
        Assert.Null(Convert(icon));
    }

    [AvaloniaTheory]
    [InlineData("☕")]
    [InlineData("♨")]
    public void Convert_SameIcon_ReturnsSharedInstance(string icon)
    {
        Assert.Same(Convert(icon), Convert(icon));
    }

    // The gear backs the settings nav button via {x:Static}; a malformed path would crash the
    // home screen on load, so prove every shared geometry parses under the Avalonia platform.
    [AvaloniaFact]
    public void Geometries_AllParse()
    {
        Assert.NotNull(BrewIconGeometry.Coffee);
        Assert.NotNull(BrewIconGeometry.Tea);
        Assert.NotNull(BrewIconGeometry.Teapot);
        Assert.NotNull(BrewIconGeometry.Cup);
        Assert.NotNull(BrewIconGeometry.Gear);
    }
}
