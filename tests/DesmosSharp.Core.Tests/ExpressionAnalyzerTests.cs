using DesmosSharp.Core.Analysis;
using DesmosSharp.Core.Parsing;

namespace DesmosSharp.Core.Tests;

public class ExpressionAnalyzerTests
{
    [Theory]
    [InlineData("y=x^2", ExpressionKind.ExplicitFunction)]
    [InlineData("y=2x+1", ExpressionKind.ExplicitFunction)]
    [InlineData("y=sin(x)", ExpressionKind.ExplicitFunction)]
    [InlineData("sin(x)", ExpressionKind.ExplicitFunction)]
    [InlineData("x=2", ExpressionKind.VerticalLine)]
    [InlineData("x^2+y^2=1", ExpressionKind.ImplicitCurve)]
    [InlineData("x=sin(y){y>0}", ExpressionKind.ImplicitCurve)]
    [InlineData("y>x+1", ExpressionKind.Inequality)]
    [InlineData("r=sin(5theta)", ExpressionKind.PolarCurve)]
    [InlineData("(sin(2t), cos(3t))", ExpressionKind.ParametricCurve)]
    [InlineData("(1,2)", ExpressionKind.Point)]
    [InlineData("(a,b)", ExpressionKind.Point)]
    [InlineData("(1,1), (2,2)", ExpressionKind.PointList)]
    [InlineData("f(x)=x^2", ExpressionKind.FunctionDefinition)]
    [InlineData("5", ExpressionKind.Number)]
    public void Kind_is_detected(string text, ExpressionKind expected)
    {
        var analysis = ExpressionAnalyzer.Analyze(Parser.Parse(text));

        Assert.Equal(expected, analysis.Kind);
    }

    [Fact]
    public void Slider_style_expression_exposes_every_free_variable()
    {
        var analysis = ExpressionAnalyzer.Analyze(Parser.Parse("y=ax+b"));

        Assert.Equal(ExpressionKind.ExplicitFunction, analysis.Kind);
        Assert.Equal(["a", "x", "b"], analysis.FreeVariables);
    }

    [Fact]
    public void Free_variables_exclude_the_assigned_target()
    {
        var analysis = ExpressionAnalyzer.Analyze(Parser.Parse("y=sin(x)+m"));

        Assert.Equal(["x", "m"], analysis.FreeVariables);
        Assert.Equal("y", analysis.AssignedVariable);
    }

    [Fact]
    public void Function_definition_has_no_free_variables()
    {
        var analysis = ExpressionAnalyzer.Analyze(Parser.Parse("f(x)=x^2"));

        Assert.Empty(analysis.FreeVariables);
        Assert.Equal("f", analysis.AssignedVariable);
    }

    [Fact]
    public void Known_functions_are_not_treated_as_variables()
    {
        var analysis = ExpressionAnalyzer.Analyze(Parser.Parse("y=f(x)"), ["f"]);

        Assert.Equal(["x"], analysis.FreeVariables);
    }

    [Fact]
    public void Constants_are_not_free_variables()
    {
        var analysis = ExpressionAnalyzer.Analyze(Parser.Parse("y=pi*x+e"));

        Assert.Equal(["x"], analysis.FreeVariables);
    }

    [Fact]
    public void Point_array_keeps_both_coordinates()
    {
        var analysis = ExpressionAnalyzer.Analyze(Parser.Parse("(1,1), (2,2)"));

        Assert.Equal(ExpressionKind.PointList, analysis.Kind);
        Assert.Empty(analysis.FreeVariables);
    }
}
