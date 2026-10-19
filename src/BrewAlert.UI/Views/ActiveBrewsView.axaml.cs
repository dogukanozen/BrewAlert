using Avalonia.Controls;

namespace BrewAlert.UI.Views;

public partial class ActiveBrewsView : UserControl
{
    public ActiveBrewsView()
    {
        InitializeComponent();
        SizeChanged += (_, e) => Classes.Set("compact", e.NewSize.Width < 640);
    }
}
