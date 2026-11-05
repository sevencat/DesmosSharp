using DesmosSharp.Graphing;
using SkiaSharp;

namespace DesmosSharp.Rendering;

/// <summary>用 SkiaSharp 绘制网格、坐标轴、刻度文字与函数曲线。</summary>
public sealed class GraphRenderer
{
    private const float AxisLabelSize = 12;
    private const float CurveStrokeWidth = 2.5f;

    private static readonly SKColor BackgroundColor = new(0xFF, 0xFF, 0xFF);
    private static readonly SKColor MinorGridColor = new(0xEE, 0xF1, 0xF5);
    private static readonly SKColor MajorGridColor = new(0xE1, 0xE6, 0xEE);
    private static readonly SKColor AxisColor = new(0x9A, 0xA3, 0xAF);
    private static readonly SKColor LabelColor = new(0x8A, 0x8F, 0x99);

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
        DrawCurves(canvas, scene);
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

    private static void DrawCurves(SKCanvas canvas, GraphRenderScene scene)
    {
        if (scene.Functions.Count == 0)
        {
            return;
        }

        var pixelWidth = Math.Max(1, (int)Math.Round(scene.Width));

        foreach (var function in scene.Functions)
        {
            var segments = FunctionSampler.Sample(
                function.Expression,
                scene.Viewport,
                pixelWidth,
                function.Parameters);
            if (segments.Count == 0)
            {
                continue;
            }

            var color = new SKColor(
                (byte)((function.ColorArgb >> 16) & 0xFF),
                (byte)((function.ColorArgb >> 8) & 0xFF),
                (byte)(function.ColorArgb & 0xFF),
                (byte)((function.ColorArgb >> 24) & 0xFF));

            using var paint = new SKPaint
            {
                Color = color,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = CurveStrokeWidth,
                IsAntialias = true,
                StrokeCap = SKStrokeCap.Round,
                StrokeJoin = SKStrokeJoin.Round,
            };

            using var path = new SKPath();

            foreach (var segment in segments)
            {
                var first = segment[0];
                var (startX, startY) = scene.Viewport.ToScreen(first.X, first.Y, scene.Width, scene.Height);
                path.MoveTo((float)startX, (float)startY);

                for (var i = 1; i < segment.Count; i++)
                {
                    var (x, y) = scene.Viewport.ToScreen(segment[i].X, segment[i].Y, scene.Width, scene.Height);
                    path.LineTo((float)x, (float)y);
                }
            }

            canvas.DrawPath(path, paint);
        }
    }
}
