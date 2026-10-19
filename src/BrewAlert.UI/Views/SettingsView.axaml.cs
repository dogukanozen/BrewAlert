using Avalonia.Controls;

namespace BrewAlert.UI.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
        SizeChanged += (_, e) =>
        {
            var compact = e.NewSize.Width < 640;
            if (Classes.Contains("compact") == compact) return;
            Classes.Set("compact", compact);
            SettingsLayout.ColumnDefinitions = ColumnDefinitions.Parse(compact ? "*" : "*,*");
            SettingsLayout.RowDefinitions = RowDefinitions.Parse(compact ? "Auto,Auto,Auto,Auto" : "Auto,Auto");
        };
    }
}
