using DesmosSharp.Core.Evaluation;
using DesmosSharp.Core.Parsing;

namespace DesmosSharp.Graphing;

/// <summary>
/// 不等式区域：与隐函数共用同一张采样网格，
/// 单元格按四角平均值判定符号并合并成矩形，边界复用等值线。
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
        double cellSize = PlotGeometryBuilder.DefaultCellSize,
        IReadOnlyDictionary<string, IReadOnlyList<Value>>? columns = null)
    {
        ArgumentNullException.ThrowIfNull(function);

        var grid = ImplicitSampler.BuildGrid(function, viewport, pixelWidth, pixelHeight, parameters, cellSize, columns);

        if (grid is null)
        {
            return PlotGeometry.Empty;
        }

        return new PlotGeometry
        {
            Fills = BuildFills(grid, comparison),
            Segments = ImplicitSampler.ExtractSegments(grid),
        };
    }

    private static List<GraphRect> BuildFills(ScalarGrid grid, CompareOperator comparison)
    {
        var fills = new List<GraphRect>();

        for (var row = 0; row < grid.Rows - 1; row++)
        {
            var runStart = -1;

            for (var column = 0; column <= grid.Columns - 1; column++)
            {
                var satisfied = false;

                if (column < grid.Columns - 1)
                {
                    var average = (grid.ValueAt(row, column) +
                        grid.ValueAt(row, column + 1) +
                        grid.ValueAt(row + 1, column) +
                        grid.ValueAt(row + 1, column + 1)) / 4;

                    satisfied = !double.IsNaN(average) && IsSatisfied(average, comparison);
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
                        grid.XMin + runStart * grid.StepX,
                        grid.XMin + column * grid.StepX,
                        grid.YMin + row * grid.StepY,
                        grid.YMin + (row + 1) * grid.StepY));

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
