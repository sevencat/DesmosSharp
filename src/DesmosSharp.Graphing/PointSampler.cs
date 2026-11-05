using DesmosSharp.Core.Evaluation;

namespace DesmosSharp.Graphing;

/// <summary>点与点列：一次求值后把得到的点摊平。</summary>
public static class PointSampler
{
    public static IReadOnlyList<GraphPoint> Sample(
        CompiledExpression expression,
        IReadOnlyDictionary<string, double>? parameters = null)
    {
        ArgumentNullException.ThrowIfNull(expression);

        var points = new List<GraphPoint>();
        Append(expression.Evaluate(PlotSampling.CreateContext(parameters)), points);
        return points;
    }

    private static void Append(Value value, List<GraphPoint> points)
    {
        switch (value.Kind)
        {
            case ValueKind.Point:
                if (PlotSampling.TryReadPoint(value, out var point))
                {
                    points.Add(point);
                }

                break;

            case ValueKind.List:
                foreach (var item in value.Items ?? [])
                {
                    Append(item, points);
                }

                break;
        }
    }
}
