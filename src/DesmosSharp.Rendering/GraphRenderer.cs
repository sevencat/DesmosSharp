using DesmosSharp.Core.Parsing;
using DesmosSharp.Graphing;
using SkiaSharp;

namespace DesmosSharp.Rendering;

/// <summary>用 SkiaSharp 绘制网格、坐标轴、刻度文字与各类图形。</summary>
public sealed class GraphRenderer
{
    private const float AxisLabelSize = 12;
    private const float CurveStrokeWidth = 2.5f;
    private const float BoundaryStrokeWidth = 2f;
    private const float PointRadius = 4.5f;
    private const byte FillAlpha = 0x38;

    private static readonly float[] DashIntervals = [5f, 4f];

    private static readonly SKColor BackgroundColor = new(0xFF, 0xFF, 0xFF);
    private static readonly SKColor MinorGridColor = new(0xEE, 0xF1, 0xF5);
    private static readonly SKColor MajorGridColor = new(0xE1, 0xE6, 0xEE);
    private static readonly SKColor AxisColor = new(0x9A, 0xA3, 0xAF);
    private static readonly SKColor LabelColor = new(0x8A, 0x8F, 0x99);

    private readonly PlotGeometryCache _cache = new();

    public void Render(SKCanvas canvas, GraphRenderScene scene)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(scene);

        if (scene.Width <= 0 || scene.Height <= 0 || !scene.Viewport.IsValid)
        {
            return;
        }

        DrawBackground(canvas, scene);

        var ticks = TickGenerator.Generate(scene.Viewport, scene.Width, scene.Height);
        DrawGrid(canvas, scene, ticks);
        DrawAxes(canvas, scene);
        DrawLabels(canvas, scene, ticks);
        DrawPlots(canvas, scene);
    }

    private static void DrawBackground(SKCanvas canvas, GraphRenderScene scene)
    {
        using var paint = new SKPaint { Color = BackgroundColor, Style = SKPaintStyle.Fill };
        canvas.DrawRect(SKRect.Create((float)scene.Width, (float)scene.Height), paint);
    }

    private static void DrawGrid(SKCanvas canvas, GraphRenderScene scene, TickSet ticks)
    {
        using var minor = new SKPaint
        {
            Color = MinorGridColor,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1,
            IsAntialias = false,
        };

        using var major = new SKPaint
        {
            Color = MajorGridColor,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1,
            IsAntialias = false,
        };

        foreach (var tick in ticks.X)
        {
            var x = (float)tick.Position;
            canvas.DrawLine(x, 0, x, (float)scene.Height, tick.IsMajor ? major : minor);
        }

        foreach (var tick in ticks.Y)
        {
            var y = (float)tick.Position;
            canvas.DrawLine(0, y, (float)scene.Width, y, tick.IsMajor ? major : minor);
        }
    }

    private static void DrawAxes(SKCanvas canvas, GraphRenderScene scene)
    {
        var viewport = scene.Viewport;

        using var paint = new SKPaint
        {
            Color = AxisColor,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1.5f,
            IsAntialias = true,
        };

        if (viewport.XMin <= 0 && viewport.XMax >= 0)
        {
            var (axisX, _) = viewport.ToScreen(0, 0, scene.Width, scene.Height);
            canvas.DrawLine((float)axisX, 0, (float)axisX, (float)scene.Height, paint);
        }

        if (viewport.YMin <= 0 && viewport.YMax >= 0)
        {
            var (_, axisY) = viewport.ToScreen(0, 0, scene.Width, scene.Height);
            canvas.DrawLine(0, (float)axisY, (float)scene.Width, (float)axisY, paint);
        }
    }

    private static void DrawLabels(SKCanvas canvas, GraphRenderScene scene, TickSet ticks)
    {
        var viewport = scene.Viewport;

        using var typeface = SKTypeface.Default;
        using var font = new SKFont(typeface, AxisLabelSize);
        using var paint = new SKPaint { Color = LabelColor, IsAntialias = true };

        var hasXAxis = viewport.YMin <= 0 && viewport.YMax >= 0;
        var hasYAxis = viewport.XMin <= 0 && viewport.XMax >= 0;

        var (originX, originY) = viewport.ToScreen(0, 0, scene.Width, scene.Height);
        var xLabelY = (float)Math.Clamp(hasXAxis ? originY + 18 : scene.Height - 6, 14, scene.Height - 4);
        var yLabelX = (float)Math.Clamp(hasYAxis ? originX - 8 : 8, 30, scene.Width - 6);

        foreach (var tick in ticks.X)
        {
            if (!tick.IsMajor || Math.Abs(tick.Value) < 1e-9)
            {
                continue;
            }

            canvas.DrawText(tick.Label, (float)tick.Position, xLabelY, SKTextAlign.Center, font, paint);
        }

        foreach (var tick in ticks.Y)
        {
            if (!tick.IsMajor || Math.Abs(tick.Value) < 1e-9)
            {
                continue;
            }

            canvas.DrawText(tick.Label, yLabelX, (float)tick.Position + 4, SKTextAlign.Right, font, paint);
        }

        if (hasXAxis && hasYAxis)
        {
            canvas.DrawText("0", (float)originX - 8, (float)originY + 18, SKTextAlign.Right, font, paint);
        }
    }

    private void DrawPlots(SKCanvas canvas, GraphRenderScene scene)
    {
        if (scene.Plots.Count == 0)
        {
            return;
        }

        foreach (var plot in scene.Plots)
        {
            var geometry = _cache.GetOrAdd(
                new PlotGeometryCache.Key(plot, scene.Viewport, scene.Width, scene.Height),
                () => PlotGeometryBuilder.Build(plot, scene.Viewport, scene.Width, scene.Height));

            if (geometry.IsEmpty)
            {
                continue;
            }

            var color = ToColor(plot.ColorArgb);
            var dashed = plot is InequalityPlot
            {
                Operator: CompareOperator.Less or CompareOperator.Greater,
            };

            DrawFills(canvas, scene, geometry.Fills, color);
            DrawPolylines(canvas, scene, geometry.Polylines, color);
            DrawSegments(canvas, scene, geometry.Segments, color, dashed);
            DrawPoints(canvas, scene, geometry.Points, color);
        }
    }

    private static void DrawFills(
        SKCanvas canvas,
        GraphRenderScene scene,
        IReadOnlyList<GraphRect> fills,
        SKColor color)
    {
        if (fills.Count == 0)
        {
            return;
        }

        using var paint = new SKPaint
        {
            Color = color.WithAlpha(FillAlpha),
            Style = SKPaintStyle.Fill,
            IsAntialias = false,
        };

        foreach (var rect in fills)
        {
            var (left, top) = scene.Viewport.ToScreen(rect.XMin, rect.YMax, scene.Width, scene.Height);
            var (right, bottom) = scene.Viewport.ToScreen(rect.XMax, rect.YMin, scene.Width, scene.Height);

            canvas.DrawRect(
                SKRect.Create((float)left, (float)top, (float)(right - left), (float)(bottom - top)),
                paint);
        }
    }

    private static void DrawPolylines(
        SKCanvas canvas,
        GraphRenderScene scene,
        IReadOnlyList<IReadOnlyList<GraphPoint>> polylines,
        SKColor color)
    {
        if (polylines.Count == 0)
        {
            return;
        }

        using var paint = CreateStrokePaint(color, CurveStrokeWidth);

        using var path = new SKPath();

        foreach (var polyline in polylines)
        {
            if (polyline.Count < 2)
            {
                continue;
            }

            var first = polyline[0];
            var (startX, startY) = scene.Viewport.ToScreen(first.X, first.Y, scene.Width, scene.Height);
            path.MoveTo((float)startX, (float)startY);

            for (var i = 1; i < polyline.Count; i++)
            {
                var (x, y) = scene.Viewport.ToScreen(polyline[i].X, polyline[i].Y, scene.Width, scene.Height);
                path.LineTo((float)x, (float)y);
            }
        }

        canvas.DrawPath(path, paint);
    }

    private static void DrawSegments(
        SKCanvas canvas,
        GraphRenderScene scene,
        IReadOnlyList<GraphSegment> segments,
        SKColor color,
        bool dashed = false)
    {
        if (segments.Count == 0)
        {
            return;
        }

        using var dash = dashed ? SKPathEffect.CreateDash(DashIntervals, 0) : null;
        using var paint = CreateStrokePaint(color, BoundaryStrokeWidth);

        if (dash is not null)
        {
            paint.PathEffect = dash;
        }

        foreach (var segment in segments)
        {
            var (ax, ay) = scene.Viewport.ToScreen(segment.A.X, segment.A.Y, scene.Width, scene.Height);
            var (bx, by) = scene.Viewport.ToScreen(segment.B.X, segment.B.Y, scene.Width, scene.Height);

            canvas.DrawLine((float)ax, (float)ay, (float)bx, (float)by, paint);
        }
    }

    private static void DrawPoints(
        SKCanvas canvas,
        GraphRenderScene scene,
        IReadOnlyList<GraphPoint> points,
        SKColor color)
    {
        if (points.Count == 0)
        {
            return;
        }

        using var halo = new SKPaint
        {
            Color = SKColors.White,
            Style = SKPaintStyle.Fill,
            IsAntialias = true,
        };

        using var fill = new SKPaint
        {
            Color = color,
            Style = SKPaintStyle.Fill,
            IsAntialias = true,
        };

        foreach (var point in points)
        {
            var (x, y) = scene.Viewport.ToScreen(point.X, point.Y, scene.Width, scene.Height);

            canvas.DrawCircle((float)x, (float)y, PointRadius + 1.5f, halo);
            canvas.DrawCircle((float)x, (float)y, PointRadius, fill);
        }
    }

    private static SKPaint CreateStrokePaint(SKColor color, float width) => new()
    {
        Color = color,
        Style = SKPaintStyle.Stroke,
        StrokeWidth = width,
        IsAntialias = true,
        StrokeCap = SKStrokeCap.Round,
        StrokeJoin = SKStrokeJoin.Round,
    };

    private static SKColor ToColor(uint argb) => new(
        (byte)((argb >> 16) & 0xFF),
        (byte)((argb >> 8) & 0xFF),
        (byte)(argb & 0xFF),
        (byte)((argb >> 24) & 0xFF));
}
