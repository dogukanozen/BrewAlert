using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using BrewAlert.UI.ViewModels;

namespace BrewAlert.UI.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
        SizeChanged += (_, e) => Classes.Set("compact", e.NewSize.Width < 640);
        AddHandler(PointerPressedEvent, OnUserInput, RoutingStrategies.Tunnel);
        AddHandler(KeyDownEvent, OnUserInput, RoutingStrategies.Tunnel);
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

    private void OnAmbientPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            vm.RecordUserActivity();
        e.Handled = true;
    }
}
