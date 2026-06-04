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

    // Logo recolours on every edge hit — the classic DVD-screensaver tell.
    private static readonly Color[] DvdColors =
    {
        Color.Parse("#f0a500"), Color.Parse("#ff6b9d"), Color.Parse("#3ddc84"),
        Color.Parse("#4aa8ff"), Color.Parse("#b06bff"), Color.Parse("#ff8a3d"),
    };

    private readonly DispatcherTimer _dvdTimer;
    private readonly TranslateTransform _dvdTransform;
    private AmbientOverlayViewModel? _vm;
    private double _velocityX = 2.6;
    private double _velocityY = 2.0;
    private int _colorIndex;

    public AmbientOverlayView()
    {
        InitializeComponent();

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
        ApplyNextColor();
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
            ApplyNextColor();
    }

    private void ApplyNextColor()
    {
        DvdLogo.Foreground = new SolidColorBrush(DvdColors[_colorIndex % DvdColors.Length]);
        _colorIndex++;
    }
}
