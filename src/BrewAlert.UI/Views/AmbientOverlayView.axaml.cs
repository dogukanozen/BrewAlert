using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using BrewAlert.UI.ViewModels;

namespace BrewAlert.UI.Views;

public partial class AmbientOverlayView : UserControl
{
    // A single TextBlock translated at ~30 fps — transform-only, cheap enough for the Pi.
    // Only runs while the DVD mode is actually on screen.
    private static readonly TimeSpan DvdFrame = TimeSpan.FromMilliseconds(33);

    // Bounce feedback changes opacity, never hue; foreground follows the active appearance.
    private static readonly double[] DvdTones = { 0.72, 0.86, 1.0 };

    private readonly DispatcherTimer _dvdTimer;
    private readonly TranslateTransform _dvdTransform;
    private AmbientOverlayViewModel? _vm;
    private double _velocityX = 2.6;
    private double _velocityY = 2.0;
    private int _toneIndex;
    private readonly Viewbox[] _orbitIcons;

    public AmbientOverlayView()
    {
        InitializeComponent();
        _orbitIcons = OrbitStage.Children.OfType<Viewbox>().ToArray();
        SizeChanged += (_, _) => UpdateScale();

        _dvdTransform = (TranslateTransform)DvdLogo.RenderTransform!;

        _dvdTimer = new DispatcherTimer { Interval = DvdFrame };
        _dvdTimer.Tick += OnDvdTick;

        DataContextChanged += OnDataContextChanged;
    }

    // Subscription lifecycle is symmetric: attach binds to the current DataContext, detach
    // tears the binding (and the timer) down. A live PropertyChanged subscription only ever
    // exists while attached, so the view can't leak onto the singleton ambient view model.
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateScale();
        BindViewModel();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _dvdTimer.Stop();
        if (_vm is not null)
            _vm.PropertyChanged -= OnViewModelPropertyChanged;
        _vm = null;
    }

    private void UpdateScale()
    {
        if (Bounds.Width <= 0 || Bounds.Height <= 0) return;
        var compact = Bounds.Width < 480;
        var iconSize = compact ? 24 : 32;
        var diameter = Math.Max(160, Math.Min(388, Math.Min(Bounds.Width - 16, Bounds.Height - 72)));
        OrbitStage.Width = OrbitStage.Height = diameter;
        OrbitRing.Margin = new Thickness(iconSize / 2.0);
        foreach (var icon in _orbitIcons) icon.Width = icon.Height = iconSize;
        AmbientClock.FontSize = compact ? 64 : 88;
        AmbientDate.FontSize = compact ? 12 : 14;
        AmbientDate.LetterSpacing = 0.5;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        // Only hold a subscription while attached; OnAttached/OnDetached own the lifecycle.
        if (this.GetVisualRoot() is not null)
            BindViewModel();
    }

    private void BindViewModel()
    {
        var vm = DataContext as AmbientOverlayViewModel;
        if (!ReferenceEquals(vm, _vm))
        {
            if (_vm is not null)
                _vm.PropertyChanged -= OnViewModelPropertyChanged;
            _vm = vm;
            if (_vm is not null)
                _vm.PropertyChanged += OnViewModelPropertyChanged;
        }
        SyncDvdTimer();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(AmbientOverlayViewModel.IsDvdMode)
            or nameof(AmbientOverlayViewModel.IsVisible))
            SyncDvdTimer();
    }

    // The bounce timer runs only while the DVD mode is actually on screen.
    private void SyncDvdTimer()
    {
        if (_vm is { IsDvdMode: true, IsVisible: true })
        {
            ResetDvdLogo();
            if (!_dvdTimer.IsEnabled)
                _dvdTimer.Start();
        }
        else
        {
            _dvdTimer.Stop();
        }
    }

    private void ResetDvdLogo()
    {
        _dvdTransform.X = 48;
        _dvdTransform.Y = 48;
        _velocityX = Math.Abs(_velocityX);
        _velocityY = Math.Abs(_velocityY);
        ApplyNextTone();
    }

    private void OnDvdTick(object? sender, EventArgs e)
    {
        var maxX = Bounds.Width - DvdLogo.Bounds.Width;
        var maxY = Bounds.Height - DvdLogo.Bounds.Height;
        if (maxX <= 0 || maxY <= 0)
            return; // Layout not measured yet — wait for the next frame.

        var x = _dvdTransform.X + _velocityX;
        var y = _dvdTransform.Y + _velocityY;
        var bounced = false;

        if (x <= 0) { x = 0; _velocityX = Math.Abs(_velocityX); bounced = true; }
        else if (x >= maxX) { x = maxX; _velocityX = -Math.Abs(_velocityX); bounced = true; }

        if (y <= 0) { y = 0; _velocityY = Math.Abs(_velocityY); bounced = true; }
        else if (y >= maxY) { y = maxY; _velocityY = -Math.Abs(_velocityY); bounced = true; }

        _dvdTransform.X = x;
        _dvdTransform.Y = y;

        if (bounced)
            ApplyNextTone();
    }

    private void ApplyNextTone()
    {
        DvdLogo.Opacity = DvdTones[_toneIndex % DvdTones.Length];
        _toneIndex++;
    }
}
