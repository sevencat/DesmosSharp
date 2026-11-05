using DesmosSharp.Core.Evaluation;

namespace DesmosSharp.Graphing;

public readonly record struct GraphPoint(double X, double Y);

/// <summary>
/// 显函数采样：按屏幕像素列求值，并在断点、无穷发散处断开线段，
/// 避免把 <c>1/x</c> 这类渐近线连成一条竖线。
/// </summary>
public static class FunctionSampler
{
    /// <summary>相邻两点纵向跳变超过视口高度的多少倍时进入细分判断。</summary>
    private const double JumpThresholdFactor = 4;

    public static IReadOnlyList<List<GraphPoint>> Sample(
        CompiledExpression expression,
        GraphViewport viewport,
        int pixelWidth,
        IReadOnlyDictionary<string, double>? parameters = null,
        IReadOnlyDictionary<string, IReadOnlyList<Value>>? columns = null)
    {
        ArgumentNullException.ThrowIfNull(expression);

        var segments = new List<List<GraphPoint>>();

        if (pixelWidth <= 0 || !viewport.IsValid)
        {
            return segments;
        }

        var context = PlotSampling.CreateContext(parameters, columns);
        var step = viewport.Width / pixelWidth;
        var jumpLimit = viewport.Height * JumpThresholdFactor;
        var valueLimit = Math.Max(viewport.Height, 1) * 1e6;

        var current = new List<GraphPoint>();

        var hasPrevious = false;
        double previousX = 0;
        double previousY = 0;

        for (var column = 0; column <= pixelWidth; column++)
        {
            var x = viewport.XMin + column * step;
            var y = Evaluate(context, expression, x);

            if (!IsUsable(y, valueLimit))
            {
                current = Flush(segments, current);
                hasPrevious = false;
                continue;
            }

            if (hasPrevious && Math.Abs(y - previousY) > jumpLimit)
            {
                // 先用中点判断是「陡但连续」还是「渐近线」
                var middleX = (previousX + x) / 2;
                var middleY = Evaluate(context, expression, middleX);

                if (!IsUsable(middleY, valueLimit) ||
                    Math.Abs(middleY) > Math.Max(Math.Abs(previousY), Math.Abs(y)))
                {
                    current = Flush(segments, current);
                }
            }

            current.Add(new GraphPoint(x, y));
            previousX = x;
            previousY = y;
            hasPrevious = true;
        }

        Flush(segments, current);
        return segments;
    }

    private static List<GraphPoint> Flush(List<List<GraphPoint>> segments, List<GraphPoint> current)
    {
        if (current.Count >= 2)
        {
            segments.Add(current);
        }

        return [];
    }

    private static bool IsUsable(double value, double limit) =>
        double.IsFinite(value) && Math.Abs(value) <= limit;

    private static double Evaluate(EvaluationContext context, CompiledExpression expression, double x)
    {
        context.SetNumber("x", x);
        return expression.Evaluate(context).TryGetNumber(out var value) ? value : double.NaN;
    }
}
