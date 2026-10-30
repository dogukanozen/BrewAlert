using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using BrewAlert.UI.Services;

namespace BrewAlert.UI.Views;

/// <summary>Quiet theme signatures. No shaders or per-frame brushes; hidden views stop ticking.</summary>
public sealed class ThemeBackdrop : Control
{
    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<ThemeBackdrop, bool>(nameof(IsActive));
    public static readonly StyledProperty<BrewTheme> AppearanceProperty =
        AvaloniaProperty.Register<ThemeBackdrop, BrewTheme>(nameof(Appearance), BrewTheme.Sumi);
    public static readonly StyledProperty<IBrush?> StrokeProperty =
        AvaloniaProperty.Register<ThemeBackdrop, IBrush?>(nameof(Stroke));

    private readonly TimeProvider _time;
    private readonly DispatcherTimer _timer;
    private readonly StreamGeometry _waves = MakeWaves();
    private readonly StreamGeometry[] _silk = new StreamGeometry[6];
    private Pen? _pen;
    private bool _attached;
    private long _timestamp;
    private double _seconds;

    public ThemeBackdrop() : this(TimeProvider.System) { }
    internal ThemeBackdrop(TimeProvider time)
    {
        _time = time;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1000.0 / 30) };
        _timer.Tick += OnTick;
        UpdateSilk();
    }

    public bool IsActive { get => GetValue(IsActiveProperty); set => SetValue(IsActiveProperty, value); }
    public BrewTheme Appearance { get => GetValue(AppearanceProperty); set => SetValue(AppearanceProperty, value); }
    public IBrush? Stroke { get => GetValue(StrokeProperty); set => SetValue(StrokeProperty, value); }
    internal bool IsRunning => _timer.IsEnabled;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        _pen = new Pen(Stroke, 1);
        SyncTimer();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _attached = false;
        _timer.Stop();
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsActiveProperty || change.Property == IsVisibleProperty)
            SyncTimer();
        if (change.Property == StrokeProperty) _pen = new Pen(Stroke, 1);
        if (change.Property == AppearanceProperty) UpdateSilk();
        if (change.Property == AppearanceProperty || change.Property == StrokeProperty) InvalidateVisual();
    }

    private void SyncTimer()
    {
        if (_attached && IsActive && IsVisible)
        {
            if (_timer.IsEnabled) return;
            _timestamp = _time.GetTimestamp();
            _timer.Start();
        }
        else _timer.Stop();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        var now = _time.GetTimestamp();
        _seconds += _time.GetElapsedTime(_timestamp, now).TotalSeconds;
        _timestamp = now;
        if (Appearance == BrewTheme.Sumi) UpdateSilk();
        InvalidateVisual();
    }

    private void UpdateSilk()
    {
        for (var line = 0; line < _silk.Length; line++)
        {
            var geometry = new StreamGeometry();
            using (var path = geometry.Open())
            {
                for (var x = -20; x <= 820; x += 8)
                {
                    var envelope = Math.Sin(Math.PI * (x + 20) / 840);
                    var y = 226 + (line - 2.5) * 27
                        + Math.Sin(x * .007 + _seconds * .13 + line * .31) * 37 * envelope
                        + Math.Sin(x * .013 - _seconds * .11) * 9;
                    var point = new Point(x, y);
                    if (x == -20) path.BeginFigure(point, false);
                    else path.LineTo(point);
                }
                path.EndFigure(false);
            }
            _silk[line] = geometry;
        }
    }

    private static StreamGeometry MakeWaves()
    {
        var geometry = new StreamGeometry();
        using var path = geometry.Open();
        for (var row = 0; row < 4; row++)
        for (var column = -1; column < 12; column++)
        for (var ring = 0; ring < 3; ring++)
        {
            var center = new Point(column * 86 + (row % 2 == 1 ? 43 : 0), 364 + row * 41);
            var radius = 39 - ring * 9;
            for (var sample = 0; sample <= 24; sample++)
            {
                var angle = Math.PI + Math.PI * sample / 24;
                var point = center + new Vector(Math.Cos(angle) * radius, Math.Sin(angle) * radius);
                if (sample == 0) path.BeginFigure(point, false);
                else path.LineTo(point);
            }
            path.EndFigure(false);
        }
        return geometry;
    }

    public override void Render(DrawingContext context)
    {
        if (Bounds.Width <= 0 || Bounds.Height <= 0 || Stroke is null || _pen is null) return;
        using (context.PushClip(new Rect(Bounds.Size)))
        using (context.PushTransform(Matrix.CreateScale(Bounds.Width / 800, Bounds.Height / 480)))
        {
            switch (Appearance)
            {
                case BrewTheme.Sumi:
                    for (var i = 0; i < _silk.Length; i++)
                        using (context.PushOpacity(.1 + i * .013))
                            context.DrawGeometry(null, _pen, _silk[i]);
                    break;
                case BrewTheme.Ibre:
                    using (context.PushOpacity(.13))
                    {
                        context.DrawEllipse(null, _pen, new Point(400, 231), 265, 157);
                        context.DrawEllipse(null, _pen, new Point(400, 231), 293, 176);
                    }
                    for (var i = 0; i < 3; i++)
                    {
                        var angle = _seconds * .085 + i * Math.Tau / 3;
                        using (context.PushOpacity(.4))
                            context.DrawEllipse(Stroke, null,
                                new Point(400 + 265 * Math.Cos(angle), 231 + 157 * Math.Sin(angle)), 2, 2);
                    }
                    break;
                case BrewTheme.Aizome:
                    using (context.PushOpacity(.08))
                        context.DrawEllipse(Stroke, null, new Point(651, 118), 35, 35);
                    using (context.PushOpacity(.075 + Math.Sin(_seconds * .14) * .02))
                    using (context.PushTransform(Matrix.CreateTranslation(Math.Sin(_seconds * .105) * 17, 0)))
                        context.DrawGeometry(null, _pen, _waves);
                    break;
            }
        }
    }
}
