using DesmosSharp.Core.Evaluation;
using DesmosSharp.Core.Parsing;

namespace DesmosSharp.Graphing.Tests;

public class ParametricSamplerTests
{
    private static readonly GraphViewport Viewport = new(-2, 2, -2, 2);

    [Fact]
    public void Unit_circle_points_lie_on_the_circle()
    {
        var expression = ExpressionCompiler.Compile("(cos(t), sin(t))");

        var segments = ParametricSampler.Sample(expression, Viewport, 400, 400, 0, Math.Tau);
        var points = Assert.Single(segments);

        Assert.True(points.Count >= 400);
        Assert.All(points, point => Assert.Equal(1, point.X * point.X + point.Y * point.Y, 9));

        // 闭合曲线：首尾应该落在同一点
        Assert.Equal(points[0].X, points[^1].X, 3);
        Assert.Equal(points[0].Y, points[^1].Y, 3);
    }

    [Fact]
    public void Undefined_steps_are_skipped()
    {
        // t < 0 时 sqrt(t) 无定义，这些步长应当被丢弃而不是连成直线
        var expression = ExpressionCompiler.Compile("(t, sqrt(t))");

        var segments = ParametricSampler.Sample(expression, Viewport, 200, 200, -2 * Math.Tau, 2 * Math.Tau);

        var segment = Assert.Single(segments);
        Assert.All(segment, point =>
        {
            Assert.True(point.X >= -1e-9);
            Assert.Equal(point.Y, Math.Sqrt(point.X), 9);
        });
    }
}

public class PolarSamplerTests
{
    private static readonly GraphViewport Viewport = new(-2, 2, -2, 2);

    [Fact]
    public void Constant_radius_draws_a_unit_circle()
    {
        var expression = ExpressionCompiler.Compile("1");

        var segments = PolarSampler.Sample(expression, Viewport, 400, 400, 0, Math.Tau);

        var points = Assert.Single(segments);
        Assert.All(points, point => Assert.Equal(1, point.X * point.X + point.Y * point.Y, 9));
        Assert.Contains(points, point => Math.Abs(point.X - 1) < 1e-9 && Math.Abs(point.Y) < 1e-9);
    }

    [Fact]
    public void Cardioid_reaches_both_extremes()
    {
        var expression = ExpressionCompiler.Compile("1+cos(theta)");

        var segments = PolarSampler.Sample(expression, Viewport, 400, 400, 0, Math.Tau);
        var points = Assert.Single(segments);

        // θ = 0 时 r = 2，θ = π 时 r = 0（回到原点）
        Assert.Contains(points, point => Math.Abs(point.X - 2) < 1e-9 && Math.Abs(point.Y) < 1e-9);
        Assert.Contains(points, point => Math.Abs(point.X) < 1e-9 && Math.Abs(point.Y) < 1e-9);

        Assert.All(points, point =>
        {
            var radius = Math.Sqrt(point.X * point.X + point.Y * point.Y);
            Assert.True(radius <= 2 + 1e-9);
        });
    }
}

public class ImplicitSamplerTests
{
    [Fact]
    public void Unit_circle_equation_produces_segments_on_the_circle()
    {
        var expression = ExpressionCompiler.Compile("x^2+y^2-1");

        var segments = ImplicitSampler.Sample(expression, new GraphViewport(-2, 2, -2, 2), 300, 300);

        Assert.True(segments.Count > 30, $"圆应被切成多条线段，实际 {segments.Count} 条");

        Assert.All(segments, segment =>
        {
            Assert.Equal(0, segment.A.X * segment.A.X + segment.A.Y * segment.A.Y - 1, 2);
            Assert.Equal(0, segment.B.X * segment.B.X + segment.B.Y * segment.B.Y - 1, 2);
        });

        // 四个象限都应被覆盖
        Assert.Contains(segments, segment => segment.A.X > 0 && segment.A.Y > 0);
        Assert.Contains(segments, segment => segment.A.X < 0 && segment.A.Y > 0);
        Assert.Contains(segments, segment => segment.A.X < 0 && segment.A.Y < 0);
        Assert.Contains(segments, segment => segment.A.X > 0 && segment.A.Y < 0);
    }

    [Fact]
    public void Equation_without_solutions_returns_nothing()
    {
        var expression = ExpressionCompiler.Compile("x^2+y^2+1");

        Assert.Empty(ImplicitSampler.Sample(expression, new GraphViewport(-2, 2, -2, 2), 200, 200));
    }
}

public class InequalitySamplerTests
{
    [Fact]
    public void Half_plane_is_filled_on_the_matching_side()
    {
        var expression = ExpressionCompiler.Compile("y-x");

        var geometry = InequalitySampler.Sample(
            expression,
            CompareOperator.Less,
            new GraphViewport(-10, 10, -10, 10),
            400,
            400);

        Assert.NotEmpty(geometry.Fills);
        Assert.NotEmpty(geometry.Segments);

        Assert.Contains(geometry.Fills, rect =>
            rect.XMin <= 5 && rect.XMax >= 5 && rect.YMin <= -5 && rect.YMax >= -5);

        Assert.DoesNotContain(geometry.Fills, rect =>
            rect.XMin <= 0 && rect.XMax >= 0 && rect.YMin <= 5 && rect.YMax >= 5);
    }
}

public class PointSamplerTests
{
    [Fact]
    public void Single_point_is_returned()
    {
        var expression = ExpressionCompiler.Compile("(1, 2)");

        var points = PointSampler.Sample(expression);

        var point = Assert.Single(points);
        Assert.Equal(1, point.X);
        Assert.Equal(2, point.Y);
    }

    [Fact]
    public void Point_list_is_flattened()
    {
        var expression = ExpressionCompiler.Compile("[(1,2), (3,4), (5,6)]");

        var points = PointSampler.Sample(expression);

        Assert.Equal(3, points.Count);
        Assert.Equal(new GraphPoint(5, 6), points[2]);
    }

    [Fact]
    public void Parameters_are_applied()
    {
        var expression = ExpressionCompiler.Compile("(a, b)");
        var parameters = new Dictionary<string, double> { ["a"] = -1, ["b"] = 3 };

        var point = Assert.Single(PointSampler.Sample(expression, parameters));

        Assert.Equal(-1, point.X);
        Assert.Equal(3, point.Y);
    }
}

public class VerticalLineSamplerTests
{
    [Fact]
    public void Constant_x_spans_the_viewport()
    {
        var expression = ExpressionCompiler.Compile("a");

        var segment = Assert.Single(
            VerticalLineSampler.Sample(
                expression,
                new GraphViewport(-10, 10, -5, 5),
                new Dictionary<string, double> { ["a"] = 3 }));

        Assert.Equal(3, segment.A.X);
        Assert.Equal(3, segment.B.X);
        Assert.Equal(-5, segment.A.Y);
        Assert.Equal(5, segment.B.Y);
    }
}
