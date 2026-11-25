using BrewAlert.Core.Events;
using BrewAlert.Infrastructure.Configuration;
using BrewAlert.Infrastructure.Hardware;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace BrewAlert.Infrastructure.Tests;

public class EvdevRfidReaderTests : IDisposable
{
    private readonly string _tempSysfs = Path.Combine(Path.GetTempPath(), $"brewalert_sysfs_{Guid.NewGuid()}");

    private sealed class MutableTimeProvider : TimeProvider
    {
        private DateTimeOffset _utcNow = new(2026, 11, 25, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _utcNow;
        public override long GetTimestamp() => _utcNow.UtcTicks;
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public void Advance(TimeSpan delta) => _utcNow = _utcNow.Add(delta);
    }

    [Fact]
    public void LinuxKeyCodeMapper_MapsDigitsLettersNumpadAndEnter()
    {
        Assert.Equal('1', LinuxKeyCodeMapper.MapKey(2, false));
        Assert.Equal('0', LinuxKeyCodeMapper.MapKey(11, false));
        Assert.Equal('9', LinuxKeyCodeMapper.MapKey(73, false));
        Assert.Equal('A', LinuxKeyCodeMapper.MapKey(30, false));
        Assert.Equal('-', LinuxKeyCodeMapper.MapKey(12, false));
        Assert.Equal('_', LinuxKeyCodeMapper.MapKey(12, true));
        Assert.True(LinuxKeyCodeMapper.IsEnter(LinuxKeyCodeMapper.KeyEnter));
        Assert.True(LinuxKeyCodeMapper.IsEnter(LinuxKeyCodeMapper.KeyKeypadEnter));
        Assert.True(LinuxKeyCodeMapper.IsShift(LinuxKeyCodeMapper.KeyLeftShift));
    }

    [Fact]
    public void EvdevDeviceDiscovery_FindsSycreaderByVendorProduct_AndIgnoresTouchscreen()
    {
        CreateSysfsEvent("event0", "10-0038 generic ft5x06 (79)", "0000", "0000");
        CreateSysfsEvent("event3", "Sycreader RFID Technology Co., Ltd SYC ID&IC USB Reader", "ffff", "0035");

        var path = EvdevDeviceDiscovery.FindReaderDevicePath(
            null,
            _tempSysfs,
            dev => dev == "/dev/input/event0" || dev == "/dev/input/event3");

        Assert.Equal("/dev/input/event3", path);
    }

    [Fact]
    public void EvdevDeviceDiscovery_PrefersConfiguredPathWhenItExists()
    {
        CreateSysfsEvent("event3", "Sycreader RFID", "ffff", "0035");

        var path = EvdevDeviceDiscovery.FindReaderDevicePath(
            "/dev/input/event9",
            _tempSysfs,
            dev => dev == "/dev/input/event9" || dev == "/dev/input/event3");

        Assert.Equal("/dev/input/event9", path);
    }

    [Fact]
    public void SubmitCardScan_NormalizesAndDebouncesDuplicateWithinOneSecond()
    {
        var options = Substitute.For<IOptionsMonitor<RfidOptions>>();
        options.CurrentValue.Returns(new RfidOptions());
        var fakeTime = new MutableTimeProvider();
        using var sut = new EvdevRfidReaderService(options, fakeTime, NullLogger<EvdevRfidReaderService>.Instance);

        var received = new List<RfidCardScannedEvent>();
        sut.CardScanned += (_, e) => received.Add(e);

        sut.SubmitCardScan("  0012ab34  ");
        sut.SubmitCardScan("0012AB34");
        fakeTime.Advance(TimeSpan.FromMilliseconds(500));
        sut.SubmitCardScan("0012ab34");
        fakeTime.Advance(TimeSpan.FromMilliseconds(600));
        sut.SubmitCardScan("0012ab34");

        Assert.Equal(2, received.Count);
        Assert.Equal("0012AB34", received[0].CardId);
        Assert.Equal("0012AB34", received[1].CardId);
    }

    private void CreateSysfsEvent(string eventName, string deviceName, string vendor, string product)
    {
        var idDir = Path.Combine(_tempSysfs, eventName, "device", "id");
        Directory.CreateDirectory(idDir);
        File.WriteAllText(Path.Combine(_tempSysfs, eventName, "device", "name"), deviceName + "\n");
        File.WriteAllText(Path.Combine(idDir, "vendor"), vendor + "\n");
        File.WriteAllText(Path.Combine(idDir, "product"), product + "\n");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempSysfs))
        {
            try { Directory.Delete(_tempSysfs, recursive: true); } catch { }
        }
    }
}
