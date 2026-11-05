using DesmosSharp.Core.Evaluation;

namespace DesmosSharp.Graphing;

/// <summary>两点之间的线段（数学坐标），用于隐函数等无法用连续折线表达的图形。</summary>
public readonly record struct GraphSegment(GraphPoint A, GraphPoint B);

/// <summary>数学坐标下的轴对齐矩形，用于不等式区域填充。</summary>
public readonly record struct GraphRect(double XMin, double XMax, double YMin, double YMax);

/// <summary>一次绘制所需的几何数据。</summary>
public sealed class PlotGeometry
{
    public static PlotGeometry Empty { get; } = new();

    /// <summary>连续折线（显函数、参数方程、极坐标）。</summary>
    public IReadOnlyList<IReadOnlyList<GraphPoint>> Polylines { get; init; } = [];

    /// <summary>离散点（点、点列）。</summary>
    public IReadOnlyList<GraphPoint> Points { get; init; } = [];

    /// <summary>孤立线段（隐函数、不等式边界）。</summary>
    public IReadOnlyList<GraphSegment> Segments { get; init; } = [];

    /// <summary>填充区域（不等式）。</summary>
    public IReadOnlyList<GraphRect> Fills { get; init; } = [];

    public bool IsEmpty =>
        Polylines.Count == 0 &&
        Points.Count == 0 &&
        Segments.Count == 0 &&
        Fills.Count == 0;
}

/// <summary>把图形元素转换为可绘制的几何数据。</summary>
public static class PlotGeometryBuilder
{
    /// <summary>隐函数与不等式使用的屏幕网格边长（像素）。</summary>
    public const double DefaultCellSize = 4;

    public static PlotGeometry Build(
        PlotItem item,
        GraphViewport viewport,
        double pixelWidth,
        double pixelHeight)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (!viewport.IsValid || pixelWidth <= 0 || pixelHeight <= 0)
        {
            return PlotGeometry.Empty;
        }

        return item switch
        {
            ExplicitPlot plot => new PlotGeometry
            {
                Polylines = FunctionSampler.Sample(
                    plot.Expression,
                    viewport,
                    Math.Max(1, (int)Math.Round(pixelWidth)),
                    plot.Parameters,
                    plot.Columns),
            },

            ParametricPlot plot => new PlotGeometry
            {
                Polylines = ParametricSampler.Sample(
                    plot.PointExpression,
                    viewport,
                    pixelWidth,
                    pixelHeight,
                    plot.TMin,
                    plot.TMax,
                    plot.Parameters,
                    plot.Columns),
            },

            PolarPlot plot => new PlotGeometry
            {
                Polylines = PolarSampler.Sample(
                    plot.RadiusExpression,
                    viewport,
                    pixelWidth,
                    pixelHeight,
                    plot.ThetaMin,
                    plot.ThetaMax,
                    plot.Parameters,
                    plot.Columns),
            },

            ImplicitPlot plot => new PlotGeometry
            {
                Segments = ImplicitSampler.Sample(
                    plot.Function,
                    viewport,
                    pixelWidth,
                    pixelHeight,
                    plot.Parameters,
                    PlotGeometryBuilder.DefaultCellSize,
                    plot.Columns),
            },

            InequalityPlot plot => InequalitySampler.Sample(
                plot.Function,
                plot.Operator,
                viewport,
                pixelWidth,
                pixelHeight,
                plot.Parameters,
                PlotGeometryBuilder.DefaultCellSize,
                plot.Columns),

            VerticalLinePlot plot => new PlotGeometry
            {
                Segments = VerticalLineSampler.Sample(plot.XExpression, viewport, plot.Parameters, plot.Columns),
            },

            PointPlot plot => new PlotGeometry
            {
                Points = PointSampler.Sample(plot.Expression, plot.Parameters, plot.Columns),
            },

            ScatterPlot plot => new PlotGeometry
            {
                Points = plot.Points,
                Polylines = plot.Connected && plot.Points.Count > 1 ? [plot.Points] : [],
            },

            // 回归曲线的参数需要先用数据求解，由上层换算成 ExplicitPlot
            RegressionPlot => PlotGeometry.Empty,

            _ => PlotGeometry.Empty,
        };
    }
}

/// <summary>采样器共用的小工具。</summary>
internal static class PlotSampling
{
    public static EvaluationContext CreateContext(
        IReadOnlyDictionary<string, double>? parameters,
        IReadOnlyDictionary<string, IReadOnlyList<Value>>? columns = null)
    {
        var context = EvaluationContext.Create();

        if (parameters is not null)
        {
            foreach (var (name, value) in parameters)
            {
                context.SetNumber(name, value);
            }
        }

        if (columns is not null)
        {
            foreach (var (name, values) in columns)
            {
                context.SetVariable(name, Value.FromList(values));
            }
        }

        return context;
    }

    public static List<GraphPoint> Flush(List<List<GraphPoint>> segments, List<GraphPoint> current)
    {
        if (current.Count >= 2)
        {
            segments.Add(current);
        }

        return [];
    }

    public static bool IsUsable(double value) => double.IsFinite(value) && Math.Abs(value) < 1e9;

    public static bool TryReadPoint(Value value, out GraphPoint point)
    {
        point = default;

        if (!value.TryGetPoint(out var raw) || !IsUsable(raw.X) || !IsUsable(raw.Y))
        {
            return false;
        }

        point = new GraphPoint(raw.X, raw.Y);
        return true;
    }
}
