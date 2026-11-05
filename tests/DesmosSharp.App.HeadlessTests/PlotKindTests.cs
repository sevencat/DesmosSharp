using Avalonia.Headless.XUnit;
using DesmosSharp.App.ViewModels;
using DesmosSharp.Graphing;

namespace DesmosSharp.App.HeadlessTests;

/// <summary>手册第 7 页的各类图形都应能被识别并生成对应的绘制元素。</summary>
public class PlotKindTests
{
    private static ExpressionItemViewModel FirstRow(string latex)
    {
        var viewModel = new MainWindowViewModel();
        var row = viewModel.Expressions[0];
        row.Latex = latex;
        return row;
    }

    [AvaloniaFact]
    public void Explicit_function_keeps_sliders()
    {
        var row = FirstRow("y=mx+b");

        Assert.IsType<ExplicitPlot>(row.Plot);
        Assert.Equal(["m", "b"], row.ParameterNames);
    }

    [AvaloniaFact]
    public void Parametric_curve_uses_t_and_creates_no_slider()
    {
        var row = FirstRow("(cos(t), sin(t))");

        var plot = Assert.IsType<ParametricPlot>(row.Plot);
        Assert.Equal(0, plot.TMin);
        Assert.Equal(Math.Tau, plot.TMax, 9);
        Assert.Empty(row.ParameterNames);
    }

    [AvaloniaFact]
    public void Parametric_curve_can_still_have_parameters()
    {
        var row = FirstRow("(a*cos(t), a*sin(t))");

        Assert.IsType<ParametricPlot>(row.Plot);
        Assert.Equal(["a"], row.ParameterNames);
    }

    [AvaloniaFact]
    public void Polar_curve_uses_theta_and_creates_no_slider()
    {
        var row = FirstRow("r=1+cos(theta)");

        var plot = Assert.IsType<PolarPlot>(row.Plot);
        Assert.Equal(0, plot.ThetaMin);
        Assert.Equal(Math.Tau, plot.ThetaMax, 9);
        Assert.Empty(row.ParameterNames);
    }

    [AvaloniaFact]
    public void Single_point_is_drawn_as_a_point()
    {
        var row = FirstRow("(1,2)");

        Assert.IsType<PointPlot>(row.Plot);
        Assert.Empty(row.ParameterNames);
    }

    [AvaloniaFact]
    public void Point_parameters_become_sliders()
    {
        var row = FirstRow("(a,b)");

        var plot = Assert.IsType<PointPlot>(row.Plot);
        Assert.Equal(["a", "b"], row.ParameterNames);

        var points = PointSampler.Sample(
            plot.Expression,
            new Dictionary<string, double> { ["a"] = 1, ["b"] = -2 });

        var point = Assert.Single(points);
        Assert.Equal(1, point.X);
        Assert.Equal(-2, point.Y);
    }

    [AvaloniaFact]
    public void Point_list_is_drawn_as_points()
    {
        var row = FirstRow("[(1,2), (3,4)]");

        var plot = Assert.IsType<PointPlot>(row.Plot);
        Assert.Equal(2, PointSampler.Sample(plot.Expression).Count);
    }

    [AvaloniaFact]
    public void Implicit_equation_is_classified()
    {
        var row = FirstRow("x^2+y^2=1");

        Assert.IsType<ImplicitPlot>(row.Plot);
        Assert.False(row.HasError);
    }

    [AvaloniaFact]
    public void Inequality_is_classified_with_its_operator()
    {
        var row = FirstRow("y<x");

        var plot = Assert.IsType<InequalityPlot>(row.Plot);
        Assert.Equal(DesmosSharp.Core.Parsing.CompareOperator.Less, plot.Operator);
    }

    [AvaloniaFact]
    public void Vertical_line_is_drawn_as_a_line()
    {
        var row = FirstRow("x=a");

        Assert.IsType<VerticalLinePlot>(row.Plot);
        Assert.Equal(["a"], row.ParameterNames);
    }

    [AvaloniaFact]
    public void Piecewise_function_is_an_explicit_plot()
    {
        var row = FirstRow("y={x<0: -x, x>=0: x}");

        var plot = Assert.IsType<ExplicitPlot>(row.Plot);

        var segments = FunctionSampler.Sample(
            plot.Expression,
            new GraphViewport(-2, 2, -2, 2),
            400);

        Assert.NotEmpty(segments);
        Assert.Contains(segments.SelectMany(segment => segment), point => point.X < 0);
        Assert.Contains(segments.SelectMany(segment => segment), point => point.X > 0);
    }

    [AvaloniaFact]
    public void Restricted_domain_breaks_the_curve()
    {
        var row = FirstRow("y=x{x>0}");

        var plot = Assert.IsType<ExplicitPlot>(row.Plot);

        var segments = FunctionSampler.Sample(
            plot.Expression,
            new GraphViewport(-2, 2, -2, 2),
            400);

        Assert.All(segments.SelectMany(segment => segment), point => Assert.True(point.X >= 0));
    }

    [AvaloniaFact]
    public void Plain_number_draws_nothing_and_reports_no_error()
    {
        var row = FirstRow("5");

        Assert.Null(row.Plot);
        Assert.False(row.HasError);
    }
}
