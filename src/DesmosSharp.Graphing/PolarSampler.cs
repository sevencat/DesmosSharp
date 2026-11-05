using DesmosSharp.Core.Evaluation;

namespace DesmosSharp.Graphing;

/// <summary>
/// 极坐标采样：<c>r = f(θ)</c>，按 θ 扫描后换算为直角坐标点。
/// </summary>
public static class PolarSampler
{
    private const int MinSteps = 720;
    private const int MaxSteps = 6000;

    public static IReadOnlyList<List<GraphPoint>> Sample(
        CompiledExpression radiusExpression,
        GraphViewport viewport,
        double pixelWidth,
        double pixelHeight,
        double thetaMin,
        double thetaMax,
        IReadOnlyDictionary<string, double>? parameters = null,
        IReadOnlyDictionary<string, IReadOnlyList<Value>>? columns = null)
    {
        ArgumentNullException.ThrowIfNull(radiusExpression);

        var segments = new List<List<GraphPoint>>();

        if (!viewport.IsValid || pixelWidth <= 0 || pixelHeight <= 0 || thetaMax <= thetaMin)
        {
            return segments;
        }

        var steps = Math.Clamp(
            (int)Math.Round(Math.Max(pixelWidth, pixelHeight) * 1.5),
            MinSteps,
            MaxSteps);

        var context = PlotSampling.CreateContext(parameters, columns);
        var jumpLimit = Math.Max(viewport.Width, viewport.Height) * 4;

        var current = new List<GraphPoint>();
        var previous = default(GraphPoint);
        var hasPrevious = false;

        for (var index = 0; index <= steps; index++)
        {
            var theta = thetaMin + (thetaMax - thetaMin) * index / steps;
            context.SetNumber("theta", theta);

            if (!radiusExpression.Evaluate(context).TryGetNumber(out var radius) || !PlotSampling.IsUsable(radius))
            {
                current = PlotSampling.Flush(segments, current);
                hasPrevious = false;
                continue;
            }

            var point = new GraphPoint(radius * Math.Cos(theta), radius * Math.Sin(theta));

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
