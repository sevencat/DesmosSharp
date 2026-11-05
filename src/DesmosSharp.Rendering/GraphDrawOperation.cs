using Avalonia;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using SkiaSharp;

namespace DesmosSharp.Rendering;

/// <summary>把一次图形绘制交给 Skia 直接执行，避免逐图元生成 Avalonia 对象。</summary>
public sealed class GraphDrawOperation : ICustomDrawOperation
{
    private readonly GraphRenderScene _scene;
    private readonly GraphRenderer _renderer;

    public GraphDrawOperation(Rect bounds, GraphRenderScene scene, GraphRenderer renderer)
    {
        Bounds = bounds;
        _scene = scene;
        _renderer = renderer;
    }

    public Rect Bounds { get; }

    public bool HitTest(Point p) => Bounds.Contains(p);

    public void Render(ImmediateDrawingContext context)
    {
        var feature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
        if (feature is null)
        {
            return;
        }

        using var lease = feature.Lease();
        var canvas = lease.SkCanvas;

        canvas.Save();
        canvas.ClipRect(SKRect.Create((float)Bounds.Width, (float)Bounds.Height));
        _renderer.Render(canvas, _scene);
        canvas.Restore();
    }

    public bool Equals(ICustomDrawOperation? other) => false;

    public void Dispose()
    {
    }
}
