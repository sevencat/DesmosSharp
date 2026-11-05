using DesmosSharp.Core.Evaluation;
using DesmosSharp.Graphing;

namespace DesmosSharp.Graphing.Tests;

public class FunctionSamplerTests
{
    private static readonly GraphViewport Viewport = GraphViewport.Default;

    [Fact]
    public void Continuous_function_is_a_single_polyline()
    {
        var expression = ExpressionCompiler.Compile("sin(x)");

        var segments = FunctionSampler.Sample(expression, Viewport, 800);

        var segment = Assert.Single(segments);
        Assert.Equal(801, segment.Count);
        Assert.Equal(Viewport.XMin, segment[0].X, 6);
        Assert.Equal(Math.Sin(Viewport.XMin), segment[0].Y, 6);
        Assert.Equal(Viewport.XMax, segment[^1].X, 6);
    }

    [Fact]
    public void Vertical_asymptote_breaks_the_line()
    {
        var expression = ExpressionCompiler.Compile("1/x");

        var segments = FunctionSampler.Sample(expression, Viewport, 800);

        Assert.True(segments.Count >= 2, "1/x 在 x=0 处应断开");
        Assert.All(segments, segment => Assert.True(segment.Count >= 2));
    }

    [Fact]
    public void Undefined_domain_produces_offsets()
    {
        var expression = ExpressionCompiler.Compile("sqrt(x)");

        var segments = FunctionSampler.Sample(expression, Viewport, 800);

        var segment = Assert.Single(segments);
        Assert.All(segment, point => Assert.True(point.X >= 0));
    }

    [Fact]
    public void Tangent_discontinuities_are_split()
    {
        var expression = ExpressionCompiler.Compile("tan(x)");

        var segments = FunctionSampler.Sample(expression, new GraphViewport(-6, 6, -6, 6), 900);

        Assert.True(segments.Count >= 3, $"tan(x) 应被切分为多段，实际 {segments.Count} 段");
    }

    [Fact]
    public void Undefined_variables_draw_nothing()
    {
        var expression = ExpressionCompiler.Compile("m*x+b");

        var segments = FunctionSampler.Sample(expression, Viewport, 400);

        Assert.Empty(segments);
    }

    [Fact]
    public void Zero_width_returns_no_segments()
    {
        var expression = ExpressionCompiler.Compile("sin(x)");

        Assert.Empty(FunctionSampler.Sample(expression, Viewport, 0));
    }
}
