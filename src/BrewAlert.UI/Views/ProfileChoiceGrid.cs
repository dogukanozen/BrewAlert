using Avalonia;
using Avalonia.Controls;

namespace BrewAlert.UI.Views;

public enum ProfileChoiceLayout { Vertical, Horizontal, Stacked }

/// <summary>Grid definitions are CLR properties; expose a styled layout for live theme changes.</summary>
public sealed class ProfileChoiceGrid : Grid
{
    public static readonly StyledProperty<ProfileChoiceLayout> LayoutProperty =
        AvaloniaProperty.Register<ProfileChoiceGrid, ProfileChoiceLayout>(nameof(Layout));

    public ProfileChoiceLayout Layout { get => GetValue(LayoutProperty); set => SetValue(LayoutProperty, value); }

    public ProfileChoiceGrid() => ApplyLayout();

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == LayoutProperty) ApplyLayout();
    }

    private void ApplyLayout()
    {
        RowDefinitions = new RowDefinitions(Layout switch
        {
            ProfileChoiceLayout.Horizontal => "Auto",
            ProfileChoiceLayout.Stacked => "*,Auto",
            _ => "Auto,*,Auto",
        });
        ColumnDefinitions = new ColumnDefinitions(Layout switch
        {
            ProfileChoiceLayout.Horizontal => "Auto,*,Auto,Auto",
            ProfileChoiceLayout.Stacked => "Auto,*,Auto",
            _ => "*,Auto",
        });
    }
}
