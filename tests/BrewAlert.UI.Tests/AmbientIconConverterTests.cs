using System.Globalization;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using BrewAlert.Core.Models;
using BrewAlert.UI.Converters;
using BrewAlert.UI.ViewModels;
using Xunit;

namespace BrewAlert.UI.Tests;

public class AmbientIconConverterTests
{
    private static object? Convert(string? key) =>
        AmbientIconConverter.Instance.Convert(key, typeof(Geometry), null, CultureInfo.InvariantCulture);

    // Geometry.Parse needs the Avalonia platform, so these run under the headless app. They
    // guard against a malformed path string crashing the overlay when it first renders on the
    // Pi — exactly the surface that used to fail silently as emoji tofu.
    [AvaloniaTheory]
    [InlineData("coffee")]
    [InlineData("tea")]
    [InlineData("teapot")]
    [InlineData("steam")]
    [InlineData("sparkle")]
    [InlineData("droplet")]
    [InlineData("cup")]
    public void Convert_KnownKey_ReturnsParsedGeometry(string key)
    {
        Assert.IsAssignableFrom<Geometry>(Convert(key));
    }

    [AvaloniaTheory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-an-icon")]
    public void Convert_UnknownKey_ReturnsNull(string? key)
    {
        Assert.Null(Convert(key));
    }

    [AvaloniaFact]
    public void Convert_SameKey_ReturnsCachedInstance()
    {
        Assert.Same(Convert("coffee"), Convert("coffee"));
    }

    // Every key a brew type maps to must resolve to a geometry — otherwise a brew of that
    // type would render as a blank icon on the overlay.
    [AvaloniaTheory]
    [InlineData(BrewType.Tea)]
    [InlineData(BrewType.Coffee)]
    [InlineData(BrewType.Custom)]
    public void IconKeyFor_EveryBrewType_ResolvesToGeometry(BrewType type)
    {
        Assert.IsAssignableFrom<Geometry>(Convert(AmbientOverlayViewModel.IconKeyFor(type)));
    }
}
