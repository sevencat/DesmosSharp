using DesmosSharp.Core.Evaluation;

namespace DesmosSharp.Graphing;

/// <summary>
/// 参数方程采样：把 <c>t</c> 在给定区间内等分求值，遇到未定义或剧烈跳变时断开折线。
/// </summary>
public static class ParametricSampler
{
    private const int MinSteps = 400;
    private const int MaxSteps = 4000;

    public static IReadOnlyList<List<GraphPoint>> Sample(
        CompiledExpression pointExpression,
        GraphViewport viewport,
        double pixelWidth,
        double pixelHeight,
        double tMin,
        double tMax,
        IReadOnlyDictionary<string, double>? parameters = null)
    {
        ArgumentNullException.ThrowIfNull(pointExpression);

        var segments = new List<List<GraphPoint>>();

        if (!viewport.IsValid || pixelWidth <= 0 || pixelHeight <= 0 || tMax <= tMin)
        {
            return segments;
        }

        var steps = Math.Clamp((int)Math.Round(Math.Max(pixelWidth, pixelHeight)), MinSteps, MaxSteps);
        var context = PlotSampling.CreateContext(parameters);
        var jumpLimit = Math.Max(viewport.Width, viewport.Height) * 4;

        var current = new List<GraphPoint>();
        var previous = default(GraphPoint);
        var hasPrevious = false;

        for (var index = 0; index <= steps; index++)
        {
            var t = tMin + (tMax - tMin) * index / steps;
            context.SetNumber("t", t);

            if (!PlotSampling.TryReadPoint(pointExpression.Evaluate(context), out var point))
            {
                current = PlotSampling.Flush(segments, current);
                hasPrevious = false;
                continue;
            }

            if (hasPrevious &&
                (Math.Abs(point.X - previous.X) > jumpLimit || Math.Abs(point.Y - previous.Y) > jumpLimit))
            {
                current = PlotSampling.Flush(segments, current);
            }

            current.Add(point);
            previous = point;
            hasPrevious = true;
        }

        PlotSampling.Flush(segments, current);
        return segments;
    }
}
