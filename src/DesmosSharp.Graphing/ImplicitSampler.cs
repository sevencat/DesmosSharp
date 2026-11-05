using DesmosSharp.Core.Evaluation;

namespace DesmosSharp.Graphing;

/// <summary>隐函数 <c>F(x, y)</c> 的采样网格，行优先存放，供等值线与区域填充共用。</summary>
internal sealed class ScalarGrid
{
    public required double[] Values { get; init; }

    public required int Rows { get; init; }

    public required int Columns { get; init; }

    public required double XMin { get; init; }

    public required double YMin { get; init; }

    public required double StepX { get; init; }

    public required double StepY { get; init; }

    public double ValueAt(int row, int column) => Values[row * Columns + column];

    public GraphPoint PointAt(int row, int column) =>
        new(XMin + column * StepX, YMin + row * StepY);
}

/// <summary>
/// 隐函数 <c>F(x, y) = 0</c> 的 marching squares 求解：
/// 在屏幕网格上对符号变化做线性插值，输出可绘制的短线段。
/// </summary>
public static class ImplicitSampler
{
    /// <summary>单帧最多处理的网格角点数，防止极端视口拖垮渲染。</summary>
    private const int MaxGridPoints = 400_000;

    public static IReadOnlyList<GraphSegment> Sample(
        CompiledExpression function,
        GraphViewport viewport,
        double pixelWidth,
        double pixelHeight,
        IReadOnlyDictionary<string, double>? parameters = null,
        double cellSize = PlotGeometryBuilder.DefaultCellSize,
        IReadOnlyDictionary<string, IReadOnlyList<Value>>? columns = null)
    {
        ArgumentNullException.ThrowIfNull(function);

        var grid = BuildGrid(function, viewport, pixelWidth, pixelHeight, parameters, cellSize, columns);
        return grid is null ? [] : ExtractSegments(grid);
    }

    internal static ScalarGrid? BuildGrid(
        CompiledExpression function,
        GraphViewport viewport,
        double pixelWidth,
        double pixelHeight,
        IReadOnlyDictionary<string, double>? parameters,
        double cellSize,
        IReadOnlyDictionary<string, IReadOnlyList<Value>>? columns = null)
    {
        if (!viewport.IsValid || pixelWidth <= 0 || pixelHeight <= 0 || cellSize <= 0)
        {
            return null;
        }

        var columnCount = Math.Max(2, (int)Math.Ceiling(pixelWidth / cellSize) + 1);
        var rowCount = Math.Max(2, (int)Math.Ceiling(pixelHeight / cellSize) + 1);

        while ((long)columnCount * rowCount > MaxGridPoints)
        {
            columnCount = Math.Max(2, columnCount / 2);
            rowCount = Math.Max(2, rowCount / 2);
        }

        var context = PlotSampling.CreateContext(parameters, columns);
        var stepX = viewport.Width / (columnCount - 1);
        var stepY = viewport.Height / (rowCount - 1);
        var values = new double[rowCount * columnCount];

        for (var row = 0; row < rowCount; row++)
        {
            var y = viewport.YMin + row * stepY;
            context.SetNumber("y", y);
            var offset = row * columnCount;

            for (var column = 0; column < columnCount; column++)
            {
                context.SetNumber("x", viewport.XMin + column * stepX);
                values[offset + column] =
                    function.Evaluate(context).TryGetNumber(out var value) && PlotSampling.IsUsable(value)
                        ? value
                        : double.NaN;
            }
        }

        return new ScalarGrid
        {
            Values = values,
            Rows = rowCount,
            Columns = columnCount,
            XMin = viewport.XMin,
            YMin = viewport.YMin,
            StepX = stepX,
            StepY = stepY,
        };
    }

    internal static List<GraphSegment> ExtractSegments(ScalarGrid grid)
    {
        var segments = new List<GraphSegment>();

        for (var row = 0; row < grid.Rows - 1; row++)
        {
            for (var column = 0; column < grid.Columns - 1; column++)
            {
                var f0 = grid.ValueAt(row, column);
                var f1 = grid.ValueAt(row, column + 1);
                var f2 = grid.ValueAt(row + 1, column + 1);
                var f3 = grid.ValueAt(row + 1, column);

                if (double.IsNaN(f0) || double.IsNaN(f1) || double.IsNaN(f2) || double.IsNaN(f3))
                {
                    continue;
                }

                var index = (f0 > 0 ? 1 : 0) | (f1 > 0 ? 2 : 0) | (f2 > 0 ? 4 : 0) | (f3 > 0 ? 8 : 0);
                if (index is 0 or 15)
                {
                    continue;
                }

                var p0 = grid.PointAt(row, column);
                var p1 = grid.PointAt(row, column + 1);
                var p2 = grid.PointAt(row + 1, column + 1);
                var p3 = grid.PointAt(row + 1, column);

                var e0 = new Lazy<GraphPoint>(() => Interpolate(p0, p1, f0, f1));
                var e1 = new Lazy<GraphPoint>(() => Interpolate(p1, p2, f1, f2));
                var e2 = new Lazy<GraphPoint>(() => Interpolate(p2, p3, f2, f3));
                var e3 = new Lazy<GraphPoint>(() => Interpolate(p3, p0, f3, f0));

                switch (index)
                {
                    case 1:
                    case 14:
                        segments.Add(new GraphSegment(e3.Value, e0.Value));
                        break;

                    case 2:
                    case 13:
                        segments.Add(new GraphSegment(e0.Value, e1.Value));
                        break;

                    case 3:
                    case 12:
                        segments.Add(new GraphSegment(e3.Value, e1.Value));
                        break;

                    case 4:
                    case 11:
                        segments.Add(new GraphSegment(e1.Value, e2.Value));
                        break;

                    case 6:
                    case 9:
                        segments.Add(new GraphSegment(e0.Value, e2.Value));
                        break;

                    case 7:
                    case 8:
                        segments.Add(new GraphSegment(e3.Value, e2.Value));
                        break;

                    default:
                        AddSaddle(segments, index, f0, f1, f2, f3, e0, e1, e2, e3);
                        break;
                }
            }
        }

        return segments;
    }

    private static void AddSaddle(
        List<GraphSegment> segments,
        int index,
        double f0,
        double f1,
        double f2,
        double f3,
        Lazy<GraphPoint> e0,
        Lazy<GraphPoint> e1,
        Lazy<GraphPoint> e2,
        Lazy<GraphPoint> e3)
    {
        // 鞍点：中心点与哪些角同号，决定两个角的连通方式
        var centerIsPositive = f0 + f1 + f2 + f3 > 0;

        // index 5 = 左上 / 右下为正；index 10 = 右上 / 左下为正
        var cornersAreSeparate = index == 5 ? centerIsPositive : !centerIsPositive;

        if (cornersAreSeparate)
        {
            segments.Add(new GraphSegment(e3.Value, e0.Value));
            segments.Add(new GraphSegment(e1.Value, e2.Value));
        }
        else
        {
            segments.Add(new GraphSegment(e0.Value, e1.Value));
            segments.Add(new GraphSegment(e2.Value, e3.Value));
        }
    }

    private static GraphPoint Interpolate(GraphPoint a, GraphPoint b, double fa, double fb)
    {
        var denominator = fa - fb;
        var t = Math.Abs(denominator) < 1e-12 ? 0.5 : fa / denominator;

        return new GraphPoint(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
    }
}
