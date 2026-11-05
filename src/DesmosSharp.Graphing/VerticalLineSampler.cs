using DesmosSharp.Core.Evaluation;

namespace DesmosSharp.Graphing;

/// <summary>竖直线 <c>x = a</c>：求值得到常数后纵贯整个视口。</summary>
public static class VerticalLineSampler
{
    public static IReadOnlyList<GraphSegment> Sample(
        CompiledExpression xExpression,
        GraphViewport viewport,
        IReadOnlyDictionary<string, double>? parameters = null,
        IReadOnlyDictionary<string, IReadOnlyList<Value>>? columns = null)
    {
        ArgumentNullException.ThrowIfNull(xExpression);

        if (!viewport.IsValid)
        {
            return [];
        }

        var context = PlotSampling.CreateContext(parameters, columns);

        if (!xExpression.Evaluate(context).TryGetNumber(out var x) || !PlotSampling.IsUsable(x))
        {
            return [];
        }

        return [new GraphSegment(new GraphPoint(x, viewport.YMin), new GraphPoint(x, viewport.YMax))];
    }
}
