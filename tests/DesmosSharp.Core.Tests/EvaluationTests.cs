using DesmosSharp.Core.Evaluation;
using DesmosSharp.Core.Parsing;

namespace DesmosSharp.Core.Tests;

public class EvaluationTests
{
    [Theory]
    [InlineData("1+2*3", 7)]
    [InlineData("-2^2", -4)]
    [InlineData("2^3^2", 512)]
    [InlineData("10-4-3", 3)]
    [InlineData("8/2/2", 2)]
    public void Precedence_matches_desmos(string text, double expected)
    {
        Assert.Equal(expected, Evaluate(text));
    }

    [Theory]
    [InlineData("2x", 3, 6)]
    [InlineData("1/2x", 4, 2)]
    [InlineData("2(x+1)", 3, 8)]
    [InlineData("(1+2)(3)", 0, 9)]
    [InlineData("x(3)", 2, 6)]
    public void Implicit_multiplication(string text, double x, double expected)
    {
        Assert.Equal(expected, Evaluate(text, ("x", x)));
    }

    [Theory]
    [InlineData("5!", 120)]
    [InlineData("sqrt(16)", 4)]
    [InlineData("abs(-3)", 3)]
    [InlineData("log(100)", 2)]
    [InlineData("ln(e)", 1)]
    [InlineData("log_2(8)", 3)]
    [InlineData("max(1,5,3)", 5)]
    [InlineData("min(4,2)", 2)]
    [InlineData("gcd(12,18)", 6)]
    [InlineData("lcm(4,6)", 12)]
    [InlineData("nCr(5,2)", 10)]
    [InlineData("nPr(5,2)", 20)]
    [InlineData("mod(7,3)", 1)]
    [InlineData("ceil(1.2)", 2)]
    [InlineData("floor(1.8)", 1)]
    [InlineData("round(2.5)", 3)]
    public void Builtin_functions(string text, double expected)
    {
        Assert.Equal(expected, Evaluate(text), 9);
    }

    [Fact]
    public void Trig_follows_radians_by_default()
    {
        Assert.Equal(1, Evaluate("sin(pi/2)"), 9);
        Assert.Equal(0, Evaluate("cos(pi/2)"), 9);
    }

    [Fact]
    public void Trig_follows_degree_mode_when_enabled()
    {
        var context = EvaluationContext.Create();
        context.AngleMode = AngleMode.Degrees;
        context.SetNumber("x", 30);

        var value = ExpressionCompiler.Compile("sin(x)").Evaluate(context);

        Assert.True(value.TryGetNumber(out var number));
        Assert.Equal(0.5, number, 9);
    }

    [Fact]
    public void Inverse_trig_returns_degrees_in_degree_mode()
    {
        var context = EvaluationContext.Create();
        context.AngleMode = AngleMode.Degrees;

        var value = ExpressionCompiler.Compile("arcsin(0.5)").Evaluate(context);

        Assert.True(value.TryGetNumber(out var number));
        Assert.Equal(30, number, 9);
    }

    [Fact]
    public void Lists_broadcast_elementwise()
    {
        var context = EvaluationContext.Create();

        var value = ExpressionCompiler.Compile("[1,2,3]+1").Evaluate(context);

        Assert.Equal(ValueKind.List, value.Kind);
        var items = value.Items!;
        Assert.Equal(3, items.Count);
        Assert.True(items[0].TryGetNumber(out var first));
        Assert.Equal(2, first, 9);
        Assert.True(items[2].TryGetNumber(out var last));
        Assert.Equal(4, last, 9);
    }

    [Fact]
    public void Points_are_values()
    {
        var value = ExpressionCompiler.Compile("(1,2)").Evaluate(EvaluationContext.Create());

        Assert.True(value.TryGetPoint(out var point));
        Assert.Equal(1, point.X);
        Assert.Equal(2, point.Y);
    }

    [Fact]
    public void Piecewise_picks_first_matching_branch()
    {
        Assert.Equal(-1, Evaluate("{x<0: -1, x>=0: 1}", ("x", -5)));
        Assert.Equal(1, Evaluate("{x<0: -1, x>=0: 1}", ("x", 5)));
    }

    [Fact]
    public void Restriction_outside_domain_is_undefined()
    {
        var context = EvaluationContext.Create();
        context.SetNumber("x", -1);

        var value = ExpressionCompiler.Compile("x^2{x>0}").Evaluate(context);

        Assert.True(value.IsUndefined);
    }

    [Fact]
    public void Unknown_variable_is_undefined()
    {
        var value = ExpressionCompiler.Compile("q + 1").Evaluate(EvaluationContext.Create());

        Assert.True(value.IsUndefined);
    }

    [Fact]
    public void User_functions_are_callable()
    {
        var context = EvaluationContext.Create();
        var definition = Assert.IsType<CompareNode>(Parser.Parse("f(x) = x^2"));
        context.DefineFunction(
            ExpressionCompiler.CreateFunction(Assert.IsType<CallNode>(definition.Left), definition.Right));

        var value = ExpressionCompiler.Compile("f(3)+1").Evaluate(context);

        Assert.True(value.TryGetNumber(out var number));
        Assert.Equal(10, number, 9);
    }

    [Fact]
    public void Function_parameters_shadow_outer_variables()
    {
        var context = EvaluationContext.Create();
        context.SetNumber("x", 100);
        var definition = Assert.IsType<CompareNode>(Parser.Parse("g(x) = x+1"));
        context.DefineFunction(
            ExpressionCompiler.CreateFunction(Assert.IsType<CallNode>(definition.Left), definition.Right));

        var value = ExpressionCompiler.Compile("g(2)").Evaluate(context);

        Assert.True(value.TryGetNumber(out var number));
        Assert.Equal(3, number, 9);
    }

    private static double Evaluate(string text, params (string Name, double Value)[] variables)
    {
        var context = EvaluationContext.Create();
        foreach (var (name, value) in variables)
        {
            context.SetNumber(name, value);
        }

        var result = ExpressionCompiler.Compile(text).Evaluate(context);
        Assert.True(result.TryGetNumber(out var number), $"\"{text}\" 的结果不是数值：{result}");
        return number;
    }
}
