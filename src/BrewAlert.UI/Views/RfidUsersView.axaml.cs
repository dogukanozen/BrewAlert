using Avalonia.Controls;

namespace BrewAlert.UI.Views;

public partial class RfidUsersView : UserControl
{
    public RfidUsersView()
    {
        InitializeComponent();
        SizeChanged += (_, e) => Classes.Set("compact", e.NewSize.Width < 640);
    }
}
