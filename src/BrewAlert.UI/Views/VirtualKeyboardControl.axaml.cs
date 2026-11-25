using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Data;
using BrewAlert.UI.Media;

namespace BrewAlert.UI.Views;

/// <summary>
/// Built-in touch virtual keyboard (Turkish Q + English + numbers/symbols) for
/// kiosk environments without an OS on-screen keyboard.
/// </summary>
public partial class VirtualKeyboardControl : UserControl
{
    private static readonly (string Lower, string Upper)[] LettersRow1 =
    [
        ("q", "Q"), ("w", "W"), ("e", "E"), ("r", "R"), ("t", "T"), ("y", "Y"),
        ("u", "U"), ("ı", "I"), ("o", "O"), ("p", "P"), ("ğ", "Ğ"), ("ü", "Ü"),
    ];

    private static readonly (string Lower, string Upper)[] LettersRow2 =
    [
        ("a", "A"), ("s", "S"), ("d", "D"), ("f", "F"), ("g", "G"), ("h", "H"),
        ("j", "J"), ("k", "K"), ("l", "L"), ("ş", "Ş"), ("i", "İ"),
    ];

    private static readonly (string Lower, string Upper)[] LettersRow3 =
    [
        ("z", "Z"), ("x", "X"), ("c", "C"), ("v", "V"), ("b", "B"),
        ("n", "N"), ("m", "M"), ("ö", "Ö"), ("ç", "Ç"),
    ];

    private static readonly string[] SymbolsRow1 =
        ["1", "2", "3", "4", "5", "6", "7", "8", "9", "0", "-", "_"];

    private static readonly string[] SymbolsRow2 =
        ["@", ".", "/", ":", ";", "(", ")", "+", "=", "?", "!"];

    private static readonly string[] SymbolsRow3 =
        ["&", "#", "%", "*", "'", "\"", ",", "~", "\\"];

    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<VirtualKeyboardControl, string>(
            nameof(Text),
            defaultValue: string.Empty,
            defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<int> MaxLengthProperty =
        AvaloniaProperty.Register<VirtualKeyboardControl, int>(nameof(MaxLength), defaultValue: 40);

    public static readonly StyledProperty<string> SpaceLabelProperty =
        AvaloniaProperty.Register<VirtualKeyboardControl, string>(nameof(SpaceLabel), defaultValue: "Space");

    public static readonly StyledProperty<string> DoneLabelProperty =
        AvaloniaProperty.Register<VirtualKeyboardControl, string>(nameof(DoneLabel), defaultValue: "Done");

    public static readonly StyledProperty<ICommand?> DoneCommandProperty =
        AvaloniaProperty.Register<VirtualKeyboardControl, ICommand?>(nameof(DoneCommand));

    private bool _isShiftActive = true;
    private bool _isSymbolsMode;
    private bool _suppressAutoShift;

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public int MaxLength
    {
        get => GetValue(MaxLengthProperty);
        set => SetValue(MaxLengthProperty, value);
    }

    public string SpaceLabel
    {
        get => GetValue(SpaceLabelProperty);
        set => SetValue(SpaceLabelProperty, value);
    }

    public string DoneLabel
    {
        get => GetValue(DoneLabelProperty);
        set => SetValue(DoneLabelProperty, value);
    }

    public ICommand? DoneCommand
    {
        get => GetValue(DoneCommandProperty);
        set => SetValue(DoneCommandProperty, value);
    }

    public bool IsShiftActive => _isShiftActive;
    public bool IsSymbolsMode => _isSymbolsMode;

    public VirtualKeyboardControl()
    {
        InitializeComponent();
        RebuildKeys();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TextProperty)
        {
            if (!_suppressAutoShift)
            {
                var current = change.GetNewValue<string>() ?? string.Empty;
                var nextShift = string.IsNullOrEmpty(current) || current.EndsWith(' ');
                if (nextShift != _isShiftActive)
                {
                    _isShiftActive = nextShift;
                    if (!_isSymbolsMode)
                        RebuildKeys();
                }
            }
        }
        else if (change.Property == SpaceLabelProperty || change.Property == DoneLabelProperty)
        {
            RebuildRow4();
        }
    }

    private void RebuildKeys()
    {
        RebuildRow1();
        RebuildRow2();
        RebuildRow3();
        RebuildRow4();
    }

    private void RebuildRow1()
    {
        Row1.Children.Clear();
        Row1.ColumnDefinitions = ColumnDefinitions.Parse("*,*,*,*,*,*,*,*,*,*,*,*");

        if (_isSymbolsMode)
        {
            for (var i = 0; i < SymbolsRow1.Length; i++)
                AddCharButton(Row1, i, SymbolsRow1[i]);
        }
        else
        {
            for (var i = 0; i < LettersRow1.Length; i++)
                AddCharButton(Row1, i, _isShiftActive ? LettersRow1[i].Upper : LettersRow1[i].Lower);
        }
    }

    private void RebuildRow2()
    {
        Row2.Children.Clear();
        Row2.ColumnDefinitions = ColumnDefinitions.Parse("*,*,*,*,*,*,*,*,*,*,*");

        if (_isSymbolsMode)
        {
            for (var i = 0; i < SymbolsRow2.Length; i++)
                AddCharButton(Row2, i, SymbolsRow2[i]);
        }
        else
        {
            for (var i = 0; i < LettersRow2.Length; i++)
                AddCharButton(Row2, i, _isShiftActive ? LettersRow2[i].Upper : LettersRow2[i].Lower);
        }
    }

    private void RebuildRow3()
    {
        Row3.Children.Clear();
        Row3.ColumnDefinitions = ColumnDefinitions.Parse("1.4*,*,*,*,*,*,*,*,*,*,1.4*");

        if (_isSymbolsMode)
        {
            var abcButton = CreateActionButton("ABC", ToggleSymbols);
            Grid.SetColumn(abcButton, 0);
            Row3.Children.Add(abcButton);

            for (var i = 0; i < SymbolsRow3.Length; i++)
                AddCharButton(Row3, i + 1, SymbolsRow3[i]);
        }
        else
        {
            var shiftButton = CreateIconActionButton(BrewIconGeometry.Shift, "Shift", ToggleShift, _isShiftActive);
            Grid.SetColumn(shiftButton, 0);
            Row3.Children.Add(shiftButton);

            for (var i = 0; i < LettersRow3.Length; i++)
                AddCharButton(Row3, i + 1, _isShiftActive ? LettersRow3[i].Upper : LettersRow3[i].Lower);
        }

        var backspaceButton = CreateIconActionButton(BrewIconGeometry.Backspace, "Backspace", Backspace, false);
        Grid.SetColumn(backspaceButton, 10);
        Row3.Children.Add(backspaceButton);
    }

    private void RebuildRow4()
    {
        Row4.Children.Clear();
        Row4.ColumnDefinitions = ColumnDefinitions.Parse("1.5*,5*,*,*,2*");

        var modeButton = CreateActionButton(_isSymbolsMode ? "ABC" : "123", ToggleSymbols);
        Grid.SetColumn(modeButton, 0);
        Row4.Children.Add(modeButton);

        var spaceButton = CreateActionButton(SpaceLabel, () => AppendText(" "));
        AutomationProperties.SetName(spaceButton, "Space");
        Grid.SetColumn(spaceButton, 1);
        Row4.Children.Add(spaceButton);

        AddCharButton(Row4, 2, ".");
        AddCharButton(Row4, 3, "-");

        var doneButton = new Button
        {
            Content = DoneLabel,
            Focusable = false,
            Classes = { "brew-primary", "vk-key-primary" },
        };
        AutomationProperties.SetName(doneButton, "Done");
        doneButton.Click += (_, _) =>
        {
            if (DoneCommand?.CanExecute(null) == true)
                DoneCommand.Execute(null);
        };
        Grid.SetColumn(doneButton, 4);
        Row4.Children.Add(doneButton);
    }

    private void AddCharButton(Grid row, int column, string value)
    {
        var btn = new Button
        {
            Content = value,
            Focusable = false,
            Classes = { "vk-key" },
        };
        btn.Click += (_, _) => AppendText(value);
        Grid.SetColumn(btn, column);
        row.Children.Add(btn);
    }

    private static Button CreateActionButton(string label, Action onClick)
    {
        var btn = new Button
        {
            Content = label,
            Focusable = false,
            Classes = { "vk-key-action" },
        };
        btn.Click += (_, _) => onClick();
        return btn;
    }

    private static Button CreateIconActionButton(
        Avalonia.Media.Geometry geometry,
        string automationName,
        Action onClick,
        bool isActive)
    {
        var path = new Avalonia.Controls.Shapes.Path
        {
            Data = geometry,
            Classes = { "brew-icon" },
        };

        var btn = new Button
        {
            Content = new Viewbox { Width = 16, Height = 16, Child = path },
            Focusable = false,
            Classes = { "vk-key-action" },
        };
        if (isActive)
            btn.Classes.Add("active");
        AutomationProperties.SetName(btn, automationName);
        btn.Click += (_, _) => onClick();
        return btn;
    }

    private void ToggleShift()
    {
        _isShiftActive = !_isShiftActive;
        if (!_isSymbolsMode)
            RebuildKeys();
    }

    private void ToggleSymbols()
    {
        _isSymbolsMode = !_isSymbolsMode;
        RebuildKeys();
    }

    private void AppendText(string value)
    {
        var current = Text ?? string.Empty;
        if (current.Length + value.Length > MaxLength) return;

        var updated = current + value;
        _suppressAutoShift = true;
        try
        {
            SetCurrentValue(TextProperty, updated);
        }
        finally
        {
            _suppressAutoShift = false;
        }

        var nextShift = updated.EndsWith(' ');
        if (nextShift != _isShiftActive)
        {
            _isShiftActive = nextShift;
            if (!_isSymbolsMode)
                RebuildKeys();
        }
    }

    private void Backspace()
    {
        var current = Text ?? string.Empty;
        if (current.Length == 0) return;

        var updated = current[..^1];
        _suppressAutoShift = true;
        try
        {
            SetCurrentValue(TextProperty, updated);
        }
        finally
        {
            _suppressAutoShift = false;
        }

        var nextShift = string.IsNullOrEmpty(updated) || updated.EndsWith(' ');
        if (nextShift != _isShiftActive)
        {
            _isShiftActive = nextShift;
            if (!_isSymbolsMode)
                RebuildKeys();
        }
    }
}
