using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using DesmosSharp.Graphing;
using DesmosSharp.Rendering;

namespace DesmosSharp.App.Controls;

/// <summary>图形画板：Skia 直绘 + 滚轮缩放（光标锚定）+ 拖拽平移。</summary>
public sealed class GraphView : Control
{
    public static readonly StyledProperty<IReadOnlyList<PlottedFunction>?> FunctionsProperty =
        AvaloniaProperty.Register<GraphView, IReadOnlyList<PlottedFunction>?>(nameof(Functions));

    private readonly GraphRenderer _renderer = new();
    private GraphViewport _viewport = GraphViewport.Default;
    private Point _lastPointer;
    private bool _isPanning;

    public GraphView()
    {
        ClipToBounds = true;
        Focusable = true;
    }

    public IReadOnlyList<PlottedFunction>? Functions
    {
        get => GetValue(FunctionsProperty);
        set => SetValue(FunctionsProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == FunctionsProperty)
        {
            InvalidateVisual();
        }
    }

    public override void Render(DrawingContext context)
    {
        var bounds = Bounds;
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        var scene = new GraphRenderScene(_viewport, bounds.Width, bounds.Height, Functions ?? []);
        context.Custom(new GraphDrawOperation(new Rect(bounds.Size), scene, _renderer));
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);

        if (e.NewSize.Width > 0 && e.NewSize.Height > 0)
        {
            _viewport = _viewport.WithSquareUnits(e.NewSize.Width, e.NewSize.Height);
            InvalidateVisual();
        }
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        var position = e.GetPosition(this);
        var factor = e.Delta.Y > 0 ? 1 / 1.2 : 1.2;

        _viewport = _viewport.ZoomAt(position.X, position.Y, factor, Bounds.Width, Bounds.Height);
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            return;
        }

        _isPanning = true;
        _lastPointer = e.GetPosition(this);
        e.Pointer.Capture(this);
        e.Handled = true;
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        if (!_isPanning)
        {
            return;
        }

        var position = e.GetPosition(this);
        _viewport = _viewport.PanByPixels(
            position.X - _lastPointer.X,
            position.Y - _lastPointer.Y,
            Bounds.Width,
            Bounds.Height);

        _lastPointer = position;
        InvalidateVisual();
        e.Handled = true;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        if (!_isPanning)
        {
            return;
        }

        _isPanning = false;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        _isPanning = false;
        base.OnPointerCaptureLost(e);
    }
}
