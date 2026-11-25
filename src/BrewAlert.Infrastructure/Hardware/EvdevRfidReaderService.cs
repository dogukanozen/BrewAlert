namespace BrewAlert.Infrastructure.Hardware;

using System.Runtime.InteropServices;
using System.Text;
using BrewAlert.Core.Events;
using BrewAlert.Core.Interfaces;
using BrewAlert.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Captures USB RFID reader scans on Linux directly from <c>/dev/input/eventX</c> with
/// <c>EVIOCGRAB</c> so card digits never trigger focused UI buttons or leak into text boxes.
/// Also provides <see cref="SubmitCardScan"/> for desktop keyboard-wedge fallback and testing.
/// </summary>
public sealed class EvdevRfidReaderService : IRfidReaderService, IDisposable
{
    private const ushort EvKey = 1;
    private const int KeyRelease = 0;
    private const int KeyPress = 1;
    private const int MinCardLength = 4;
    private const int OpenReadOnly = 0;
    private const nuint Eviocgrab = 0x40044590;
    private const short PollIn = 0x0001;
    private const short PollErr = 0x0008;
    private const short PollHup = 0x0010;
    private const int PollTimeoutMs = 500;
    private const int RediscoveryDelayMs = 2000;

    private static readonly TimeSpan StaleBufferTimeout = TimeSpan.FromMilliseconds(1500);
    private static readonly TimeSpan DuplicateDebounceWindow = TimeSpan.FromSeconds(1);

    private readonly IOptionsMonitor<RfidOptions> _options;
    private readonly TimeProvider _time;
    private readonly ILogger<EvdevRfidReaderService> _logger;
    private readonly CancellationTokenSource _cts = new();
    private readonly object _lock = new();
    private readonly Thread? _readerThread;

    private string _lastCardId = string.Empty;
    private long _lastScanTimestamp;
    private bool _hasLastScan;
    private volatile bool _isHardwareReaderConnected;
    private bool _disposed;

    public bool IsHardwareReaderConnected => _isHardwareReaderConnected;
    public event EventHandler<RfidCardScannedEvent>? CardScanned;

    public EvdevRfidReaderService(
        IOptionsMonitor<RfidOptions> options,
        TimeProvider timeProvider,
        ILogger<EvdevRfidReaderService> logger)
    {
        _options = options;
        _time = timeProvider;
        _logger = logger;

        if (OperatingSystem.IsLinux())
        {
            _readerThread = new Thread(RunReaderLoop)
            {
                IsBackground = true,
                Name = "RfidEvdevReader",
            };
            _readerThread.Start();
        }
    }

    public void SubmitCardScan(string cardId)
    {
        if (string.IsNullOrWhiteSpace(cardId)) return;
        var normalized = cardId.Trim().ToUpperInvariant();
        if (normalized.Length == 0) return;

        var nowTicks = _time.GetTimestamp();
        var nowUtc = _time.GetUtcNow().UtcDateTime;

        lock (_lock)
        {
            if (_hasLastScan &&
                string.Equals(_lastCardId, normalized, StringComparison.Ordinal))
            {
                var elapsed = _time.GetElapsedTime(_lastScanTimestamp, nowTicks);
                if (elapsed < DuplicateDebounceWindow)
                    return;
            }

            _lastCardId = normalized;
            _lastScanTimestamp = nowTicks;
            _hasLastScan = true;
        }

        // Fire events OUTSIDE the lock (AGENT.md §4.2).
        CardScanned?.Invoke(this, new RfidCardScannedEvent(normalized, nowUtc));
    }

    private void RunReaderLoop()
    {
        var ct = _cts.Token;
        while (!ct.IsCancellationRequested)
        {
            string? devicePath = null;
            try
            {
                devicePath = EvdevDeviceDiscovery.FindReaderDevicePath(_options.CurrentValue.DevicePath);
                if (devicePath is null)
                {
                    _isHardwareReaderConnected = false;
                    WaitInterval(RediscoveryDelayMs, ct);
                    continue;
                }

                ReadDeviceUntilErrorOrCancel(devicePath, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "RFID evdev reader loop error for device {DevicePath}.", devicePath ?? "(none)");
            }
            finally
            {
                _isHardwareReaderConnected = false;
            }

            if (!ct.IsCancellationRequested)
                WaitInterval(RediscoveryDelayMs, ct);
        }
    }

    private void ReadDeviceUntilErrorOrCancel(string devicePath, CancellationToken ct)
    {
        var fd = LibcOpen(devicePath, OpenReadOnly);
        if (fd < 0)
        {
            var errno = Marshal.GetLastPInvokeError();
            _logger.LogWarning("Could not open RFID device {DevicePath} (errno {Errno}).", devicePath, errno);
            return;
        }

        var grabbed = false;
        try
        {
            if (LibcIoctl(fd, Eviocgrab, 1) == 0)
            {
                grabbed = true;
                _logger.LogInformation("RFID reader exclusively grabbed at {DevicePath}.", devicePath);
            }
            else
            {
                var errno = Marshal.GetLastPInvokeError();
                _logger.LogWarning("EVIOCGRAB failed for {DevicePath} (errno {Errno}); reading non-exclusively.", devicePath, errno);
            }

            _isHardwareReaderConnected = true;

            var eventSize = IntPtr.Size * 2 + 8;
            var buffer = new byte[eventSize];
            var cardBuffer = new StringBuilder(32);
            var shiftHeld = false;
            long lastKeyTimestamp = 0;

            while (!ct.IsCancellationRequested)
            {
                var pfd = new PollFd { Fd = fd, Events = PollIn, Revents = 0 };
                var pollResult = LibcPoll(ref pfd, 1, PollTimeoutMs);
                if (pollResult < 0)
                {
                    var errno = Marshal.GetLastPInvokeError();
                    if (errno == 4) continue; // EINTR
                    _logger.LogWarning("poll() failed on {DevicePath} (errno {Errno}).", devicePath, errno);
                    break;
                }

                if (pollResult == 0) continue;
                if ((pfd.Revents & (PollErr | PollHup)) != 0)
                {
                    _logger.LogWarning("RFID reader {DevicePath} disconnected (revents=0x{Revents:X}).", devicePath, pfd.Revents);
                    break;
                }

                if ((pfd.Revents & PollIn) == 0) continue;

                var bytesRead = LibcRead(fd, buffer, (nuint)eventSize);
                if (bytesRead <= 0)
                {
                    _logger.LogWarning("EOF/read failure on RFID reader {DevicePath}.", devicePath);
                    break;
                }
                if (bytesRead < eventSize) continue;

                var offset = IntPtr.Size * 2;
                var type = BitConverter.ToUInt16(buffer, offset);
                var code = BitConverter.ToUInt16(buffer, offset + 2);
                var value = BitConverter.ToInt32(buffer, offset + 4);

                if (type != EvKey) continue;

                if (LinuxKeyCodeMapper.IsShift(code))
                {
                    shiftHeld = value != KeyRelease;
                    continue;
                }

                if (value != KeyPress) continue;

                var nowTicks = _time.GetTimestamp();
                if (cardBuffer.Length > 0 && _time.GetElapsedTime(lastKeyTimestamp, nowTicks) > StaleBufferTimeout)
                    cardBuffer.Clear();
                lastKeyTimestamp = nowTicks;

                if (LinuxKeyCodeMapper.IsEnter(code))
                {
                    if (cardBuffer.Length >= MinCardLength)
                        SubmitCardScan(cardBuffer.ToString());
                    cardBuffer.Clear();
                    continue;
                }

                var ch = LinuxKeyCodeMapper.MapKey(code, shiftHeld);
                if (ch is not null && cardBuffer.Length < 64)
                    cardBuffer.Append(ch.Value);
            }
        }
        finally
        {
            _isHardwareReaderConnected = false;
            if (grabbed) _ = LibcIoctl(fd, Eviocgrab, 0);
            _ = LibcClose(fd);
        }
    }

    private static void WaitInterval(int milliseconds, CancellationToken ct) =>
        ct.WaitHandle.WaitOne(milliseconds);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _cts.Cancel();
        _readerThread?.Join(TimeSpan.FromSeconds(2));
        _cts.Dispose();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PollFd
    {
        public int Fd;
        public short Events;
        public short Revents;
    }

    [DllImport("libc", EntryPoint = "open", SetLastError = true, CharSet = CharSet.Ansi)]
    private static extern int LibcOpen(string pathname, int flags);

    [DllImport("libc", EntryPoint = "close", SetLastError = true)]
    private static extern int LibcClose(int fd);

    [DllImport("libc", EntryPoint = "read", SetLastError = true)]
    private static extern nint LibcRead(int fd, [Out] byte[] buf, nuint count);

    [DllImport("libc", EntryPoint = "ioctl", SetLastError = true)]
    private static extern int LibcIoctl(int fd, nuint request, int arg);

    [DllImport("libc", EntryPoint = "poll", SetLastError = true)]
    private static extern int LibcPoll(ref PollFd fds, nuint nfds, int timeout);
}
