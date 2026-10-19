using System.Globalization;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Automation;
using Avalonia.Media;
using Avalonia.Styling;

namespace BrewAlert.UI.Views;

/// <summary>A clipped, staggered entrance; minute changes move only the changed digits.</summary>
public sealed class AnimatedClock : TemplatedControl
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<AnimatedClock, string?>(nameof(Text));
    public static readonly StyledProperty<double> LetterSpacingProperty =
        AvaloniaProperty.Register<AnimatedClock, double>(nameof(LetterSpacing));
    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<AnimatedClock, bool>(nameof(IsActive));
    public static readonly StyledProperty<double> ProgressProperty =
        AvaloniaProperty.Register<AnimatedClock, double>(nameof(Progress), 1);

    private readonly Dictionary<char, FormattedText> _glyphs = new();
    private CancellationTokenSource? _transition;
    private string _previousText = string.Empty;
    private bool _attached;
    private bool _entering;
    private bool _hasEntered;
    private double _digitWidth;
    private double _height;

    static AnimatedClock() => AffectsRender<AnimatedClock>(ProgressProperty);

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public double LetterSpacing
    {
        get => GetValue(LetterSpacingProperty);
        set => SetValue(LetterSpacingProperty, value);
    }

    public bool IsActive
    {
        get => GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    public double Progress
    {
        get => GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    internal bool IsTransitionRunning => _transition is not null;
    internal int ChangedDigitCount => (Text ?? string.Empty).Where((c, i) =>
        c != ':' && (i >= _previousText.Length || _previousText[i] != c)).Count();

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        ReplayEntrance();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _attached = false;
        _hasEntered = false;
        StopTransition();
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty)
        {
            AutomationProperties.SetName(this, Text);
            _previousText = change.GetOldValue<string?>() ?? string.Empty;
            InvalidateMeasure();
            if (_hasEntered) BeginTransition(false);
            else ReplayEntrance();
        }
        else if (change.Property == IsActiveProperty)
        {
            if (IsActive) ReplayEntrance();
            else { _hasEntered = false; StopTransition(); }
        }
        else if (change.Property == FontSizeProperty || change.Property == FontFamilyProperty
            || change.Property == FontWeightProperty || change.Property == FontStyleProperty
            || change.Property == FontStretchProperty || change.Property == ForegroundProperty
            || change.Property == LetterSpacingProperty)
        {
            _glyphs.Clear();
            InvalidateMeasure();
            InvalidateVisual();
        }
    }

    public void ReplayEntrance()
    {
        if (!_attached || !IsActive || string.IsNullOrEmpty(Text)) return;
        _hasEntered = true;
        BeginTransition(true);
    }

    private void BeginTransition(bool entering)
    {
        StopTransition();
        if (!_attached || !IsActive || string.IsNullOrEmpty(Text)) return;
        _entering = entering;
        var source = new CancellationTokenSource();
        _transition = source;
        SetCurrentValue(ProgressProperty, 0.0);
        _ = AnimateAsync(source, entering ? 880 : 450);
    }

    private async Task AnimateAsync(CancellationTokenSource source, int milliseconds)
    {
        try
        {
            var animation = new Animation
            {
                Duration = TimeSpan.FromMilliseconds(milliseconds),
                Children =
                {
                    new KeyFrame { Cue = new Cue(0), Setters = { new Setter(ProgressProperty, 0.0) } },
                    new KeyFrame { Cue = new Cue(1), Setters = { new Setter(ProgressProperty, 1.0) } }
                }
            };
            await animation.RunAsync(this, cancellationToken: source.Token);
        }
        catch (OperationCanceledException) { }
        finally
        {
            if (ReferenceEquals(_transition, source))
            {
                _transition = null;
                SetCurrentValue(ProgressProperty, 1.0);
                InvalidateVisual();
            }
            source.Dispose();
        }
    }

    private void StopTransition()
    {
        var source = _transition;
        _transition = null;
        source?.Cancel();
        SetCurrentValue(ProgressProperty, 1.0);
        InvalidateVisual();
    }

    private FormattedText Glyph(char value)
    {
        if (_glyphs.TryGetValue(value, out var text)) return text;
        text = new FormattedText(value.ToString(), CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, new Typeface(FontFamily, FontStyle, FontWeight, FontStretch),
            FontSize, Foreground);
        _glyphs[value] = text;
        return text;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _digitWidth = Enumerable.Range('0', 10).Max(i => Glyph((char)i).WidthIncludingTrailingWhitespace);
        _height = Glyph('0').Height;
        var text = Text ?? string.Empty;
        var width = text.Sum(c => c == ':' ? Glyph(c).WidthIncludingTrailingWhitespace : _digitWidth)
            + Math.Max(0, text.Length - 1) * LetterSpacing;
        return new Size(Math.Max(0, width), _height);
    }

    public override void Render(DrawingContext context)
    {
        var text = Text ?? string.Empty;
        double x = 0;
        var digitIndex = 0;
        using var clip = context.PushClip(new Rect(Bounds.Size));
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            var width = c == ':' ? Glyph(c).WidthIncludingTrailingWhitespace : _digitWidth;
            if (_entering && IsTransitionRunning)
            {
                var p = Ease((Progress * 880 - (c == ':' ? 90 : digitIndex * 45)) / (c == ':' ? 650 : 700));
                DrawGlyph(context, c, x, width, _height * .15 * (1 - p), p);
            }
            else if (IsTransitionRunning && c != ':' && (i >= _previousText.Length || c != _previousText[i]))
            {
                var p = Ease(Progress);
                if (i < _previousText.Length)
                    DrawGlyph(context, _previousText[i], x, width, -_height * .24 * p, 1 - p);
                DrawGlyph(context, c, x, width, _height * .24 * (1 - p), p);
            }
            else DrawGlyph(context, c, x, width, 0, 1);
            x += width + LetterSpacing;
            if (c != ':') digitIndex++;
        }
    }

    private void DrawGlyph(DrawingContext context, char c, double x, double width, double y, double opacity)
    {
        var glyph = Glyph(c);
        using (context.PushOpacity(opacity))
            context.DrawText(glyph, new Point(x + (width - glyph.WidthIncludingTrailingWhitespace) / 2, y));
    }

    private static double Ease(double progress) => 1 - Math.Pow(1 - Math.Clamp(progress, 0, 1), 3);
}
