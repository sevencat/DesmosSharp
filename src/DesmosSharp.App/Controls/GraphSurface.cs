using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace DesmosSharp.App.Controls;

/// <summary>
/// 图形画板的临时占位控件：只绘制网格与坐标轴，用于验证渲染链路。
/// P2 阶段会被基于 SkiaSharp 的渲染器替换。
/// </summary>
public sealed class GraphSurface : Control
{
    private static readonly IBrush BackgroundBrush = new SolidColorBrush(Color.Parse("#FFFFFF"));
    private static readonly IPen MinorGridPen = new Pen(new SolidColorBrush(Color.Parse("#EEF1F5")), 1);
    private static readonly IPen MajorGridPen = new Pen(new SolidColorBrush(Color.Parse("#E1E6EE")), 1);
    private static readonly IPen AxisPen = new Pen(new SolidColorBrush(Color.Parse("#9AA3AF")), 1.5);

    private const double MinorStep = 32;
    private const int MajorEvery = 5;

    public override void Render(DrawingContext context)
    {
        var bounds = Bounds;
        context.FillRectangle(BackgroundBrush, new Rect(bounds.Size));

        var center = new Point(bounds.Width / 2, bounds.Height / 2);

        // 竖向网格
        var index = 0;
        for (var x = center.X; x <= bounds.Width; x += MinorStep, index++)
        {
            context.DrawLine(index % MajorEvery == 0 ? MajorGridPen : MinorGridPen,
                new Point(x, 0), new Point(x, bounds.Height));
        }

        index = 0;
        for (var x = center.X; x >= 0; x -= MinorStep, index++)
        {
            context.DrawLine(index % MajorEvery == 0 ? MajorGridPen : MinorGridPen,
                new Point(x, 0), new Point(x, bounds.Height));
        }

        // 横向网格
        index = 0;
        for (var y = center.Y; y <= bounds.Height; y += MinorStep, index++)
        {
            context.DrawLine(index % MajorEvery == 0 ? MajorGridPen : MinorGridPen,
                new Point(0, y), new Point(bounds.Width, y));
        }

        index = 0;
        for (var y = center.Y; y >= 0; y -= MinorStep, index++)
        {
            context.DrawLine(index % MajorEvery == 0 ? MajorGridPen : MinorGridPen,
                new Point(0, y), new Point(bounds.Width, y));
        }

        context.DrawLine(AxisPen, new Point(0, center.Y), new Point(bounds.Width, center.Y));
        context.DrawLine(AxisPen, new Point(center.X, 0), new Point(center.X, bounds.Height));
    }
}
