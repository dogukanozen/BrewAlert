using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace BrewAlert.UI.Views;

/// <summary>Six quiet lines, sampled at 30 fps only while Silk is on screen.</summary>
public sealed class SilkBackdrop : Control
{
    public static readonly StyledProperty<bool> IsActiveProperty =
        AvaloniaProperty.Register<SilkBackdrop, bool>(nameof(IsActive));
    public static readonly StyledProperty<IBrush?> StrokeProperty =
        AvaloniaProperty.Register<SilkBackdrop, IBrush?>(nameof(Stroke));

    private const int Samples = 109;
    private static readonly (double Sin, double Cos, double SlowSin, double SlowCos)[] Wave =
        Enumerable.Range(0, Samples).Select(i =>
        {
            var x = -30 + i * 8;
            return (Math.Sin(x / 160.0), Math.Cos(x / 160.0),
                Math.Sin(x / 290.0), Math.Cos(x / 290.0));
        }).ToArray();

    private readonly TimeProvider _time;
    private readonly DispatcherTimer _timer;
    private readonly StreamGeometry[] _lines = new StreamGeometry[6];
    private readonly Pen[] _pens = new Pen[6];
    private bool _attached;
    private long _lastTimestamp;
    private double _seconds;

    public SilkBackdrop() : this(TimeProvider.System) { }

    internal SilkBackdrop(TimeProvider time)
    {
        _time = time;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1000.0 / 30) };
        _timer.Tick += OnTick;
        UpdateLines();
    }

    public bool IsActive
    {
        get => GetValue(IsActiveProperty);
        set => SetValue(IsActiveProperty, value);
    }

    public IBrush? Stroke
    {
        get => GetValue(StrokeProperty);
        set => SetValue(StrokeProperty, value);
    }

    internal bool IsRunning => _timer.IsEnabled;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _attached = true;
        UpdatePens();
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
        if (change.Property == StrokeProperty)
            UpdatePens();
    }

    private void SyncTimer()
    {
        if (_attached && IsActive && IsVisible)
        {
            if (_timer.IsEnabled) return;
            _lastTimestamp = _time.GetTimestamp();
            _timer.Start();
        }
        else _timer.Stop();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        var now = _time.GetTimestamp();
        _seconds += _time.GetElapsedTime(_lastTimestamp, now).TotalSeconds;
        _lastTimestamp = now;
        UpdateLines();
        InvalidateVisual();
    }

    private void UpdatePens()
    {
        for (var j = 0; j < _pens.Length; j++)
            _pens[j] = new Pen(Stroke, 1.15);
        InvalidateVisual();
    }

    private void UpdateLines()
    {
        var phase = _seconds * Math.Tau / 18;
        var slowSin = Math.Sin(phase * .7);
        var slowCos = Math.Cos(phase * .7);
        for (var j = 0; j < _lines.Length; j++)
        {
            var sin = Math.Sin(-phase + j * .18);
            var cos = Math.Cos(-phase + j * .18);
            var line = new StreamGeometry();
            using (var path = line.Open())
            {
                for (var i = 0; i < Samples; i++)
                {
                    var w = Wave[i];
                    var y = 330 + j * 11 + (w.Sin * cos + w.Cos * sin) * (22 + j * 1.8)
                        + (w.SlowSin * slowCos + w.SlowCos * slowSin) * 11;
                    var point = new Point(-30 + i * 8, y);
                    if (i == 0) path.BeginFigure(point, false);
                    else path.LineTo(point);
                }
                path.EndFigure(false);
            }
            _lines[j] = line;
        }
    }

    public override void Render(DrawingContext context)
    {
        if (Bounds.Width <= 0 || Bounds.Height <= 0 || Stroke is null) return;
        using (context.PushClip(new Rect(Bounds.Size)))
        using (context.PushTransform(Matrix.CreateScale(Bounds.Width / 800, Bounds.Height / 480)))
            for (var j = 0; j < _lines.Length; j++)
                using (context.PushOpacity((.16 + j * .038) * .65))
                    context.DrawGeometry(null, _pens[j], _lines[j]);
    }
}
