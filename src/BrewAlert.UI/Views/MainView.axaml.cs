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
        AddHandler(PointerPressedEvent, OnUserInput, RoutingStrategies.Tunnel);
        AddHandler(KeyDownEvent, OnUserInput, RoutingStrategies.Tunnel);
    }

    private void OnUserInput(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            vm.RecordUserActivity();
    }

    private void OnAmbientPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            vm.RecordUserActivity();
        e.Handled = true;
    }
}
