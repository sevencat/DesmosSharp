using DesmosSharp.Core.Evaluation;
using DesmosSharp.Core.Parsing;

namespace DesmosSharp.Graphing;

/// <summary>
/// 不等式区域：按屏幕网格逐行判定符号，把连续满足的单元格合并成矩形；
/// 边界复用隐函数求解的等值线。
/// </summary>
public static class InequalitySampler
{
    public static PlotGeometry Sample(
        CompiledExpression function,
        CompareOperator comparison,
        GraphViewport viewport,
        double pixelWidth,
        double pixelHeight,
        IReadOnlyDictionary<string, double>? parameters = null,
        double cellSize = PlotGeometryBuilder.DefaultCellSize)
    {
        ArgumentNullException.ThrowIfNull(function);

        if (!viewport.IsValid || pixelWidth <= 0 || pixelHeight <= 0 || cellSize <= 0)
        {
            return PlotGeometry.Empty;
        }

        var boundary = ImplicitSampler.Sample(function, viewport, pixelWidth, pixelHeight, parameters, cellSize);
        var fills = SampleFills(function, comparison, viewport, pixelWidth, pixelHeight, parameters, cellSize);

        return new PlotGeometry { Fills = fills, Segments = boundary };
    }

    private static List<GraphRect> SampleFills(
        CompiledExpression function,
        CompareOperator comparison,
        GraphViewport viewport,
        double pixelWidth,
        double pixelHeight,
        IReadOnlyDictionary<string, double>? parameters,
        double cellSize)
    {
        var fills = new List<GraphRect>();
        var context = PlotSampling.CreateContext(parameters);

        var columns = Math.Max(1, (int)Math.Ceiling(pixelWidth / cellSize));
        var rows = Math.Max(1, (int)Math.Ceiling(pixelHeight / cellSize));
        var cellWidth = viewport.Width / columns;
        var cellHeight = viewport.Height / rows;

        for (var row = 0; row < rows; row++)
        {
            var y = viewport.YMin + (row + 0.5) * cellHeight;
            context.SetNumber("y", y);

            var runStart = -1;

            for (var column = 0; column <= columns; column++)
            {
                var satisfied = false;

                if (column < columns)
                {
                    var x = viewport.XMin + (column + 0.5) * cellWidth;
                    context.SetNumber("x", x);

                    satisfied = function.Evaluate(context).TryGetNumber(out var value) &&
                        PlotSampling.IsUsable(value) &&
                        IsSatisfied(value, comparison);
                }

                if (satisfied)
                {
                    if (runStart < 0)
                    {
                        runStart = column;
                    }

                    continue;
                }

                if (runStart >= 0)
                {
                    fills.Add(new GraphRect(
                        viewport.XMin + runStart * cellWidth,
                        viewport.XMin + column * cellWidth,
                        y - cellHeight / 2,
                        y + cellHeight / 2));

                    runStart = -1;
                }
            }
        }

        return fills;
    }

    private static bool IsSatisfied(double value, CompareOperator comparison) => comparison switch
    {
        CompareOperator.Less => value < 0,
        CompareOperator.LessOrEqual => value <= 0,
        CompareOperator.Greater => value > 0,
        CompareOperator.GreaterOrEqual => value >= 0,
        _ => false,
    };
}
