using System.Text;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using BrewAlert.UI.Services;
using BrewAlert.UI.ViewModels;

namespace BrewAlert.UI.Views;

public partial class MainView : UserControl
{
    private static readonly TimeSpan CardBufferTimeout = TimeSpan.FromMilliseconds(1500);
    private readonly StringBuilder _cardBuffer = new(32);
    private DateTime _lastCardKeyUtc;

    public MainView()
    {
        InitializeComponent();
        SizeChanged += (_, e) => Classes.Set("compact", e.NewSize.Width < 640);
        AddHandler(PointerPressedEvent, OnUserInput, RoutingStrategies.Tunnel);
        AddHandler(KeyDownEvent, OnKeyDownTunnel, RoutingStrategies.Tunnel);
    }

    private void OnUserInput(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
        {
            var dismissingAmbient = vm.Ambient.IsVisible || vm.Ambient.ShowPending;
            vm.RecordUserActivity();
            if (dismissingAmbient) e.Handled = true;
        }
    }

    private void OnKeyDownTunnel(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;

        var dismissingAmbient = vm.Ambient.IsVisible || vm.Ambient.ShowPending;
        vm.RecordUserActivity();
        if (dismissingAmbient)
        {
            e.Handled = true;
            return;
        }

        // Desktop / non-evdev fallback: capture keyboard-wedge RFID reader input ONLY when
        // a view can accept a card scan and no hardware evdev grab is active.
        if (vm.IsHardwareRfidConnected || vm.CurrentView is not IRfidCardScanAwaiter { CanAcceptKeyboardCardScan: true })
            return;

        if (e.Key is Key.Enter or Key.Return)
        {
            if (_cardBuffer.Length >= 4)
                vm.SubmitKeyboardCardScan(_cardBuffer.ToString());
            _cardBuffer.Clear();
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            _cardBuffer.Clear();
            return;
        }

        var now = DateTime.UtcNow;
        if (_cardBuffer.Length > 0 && now - _lastCardKeyUtc > CardBufferTimeout)
            _cardBuffer.Clear();
        _lastCardKeyUtc = now;

        var ch = MapKeyToCardChar(e.Key);
        if (ch is not null && _cardBuffer.Length < 64)
        {
            _cardBuffer.Append(ch.Value);
            e.Handled = true;
        }
    }

    private static char? MapKeyToCardChar(Key key)
    {
        if (key is >= Key.D0 and <= Key.D9)
            return (char)('0' + (key - Key.D0));
        if (key is >= Key.NumPad0 and <= Key.NumPad9)
            return (char)('0' + (key - Key.NumPad0));
        if (key is >= Key.A and <= Key.Z)
            return (char)('A' + (key - Key.A));
        if (key is Key.OemMinus or Key.Subtract)
            return '-';
        return null;
    }

    private void OnAmbientPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            vm.RecordUserActivity();
        e.Handled = true;
    }
}
