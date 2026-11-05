using DesmosSharp.Core.Analysis;
using DesmosSharp.Core.Parsing;

namespace DesmosSharp.Core.Tests;

public class RegressionParserTests
{
    [Fact]
    public void Tilde_creates_a_regression_node()
    {
        var node = Parser.Parse("y_1 ~ m*x_1 + b");

        var regression = Assert.IsType<RegressionNode>(node);
        Assert.IsType<IdentifierNode>(regression.Target);
        Assert.IsType<BinaryNode>(regression.Model);
    }

    [Fact]
    public void Regression_is_detected_by_the_analyzer()
    {
        var analysis = ExpressionAnalyzer.Analyze(Parser.Parse("y_1 ~ m*x_1 + b"));

        Assert.Equal(ExpressionKind.Regression, analysis.Kind);
        Assert.Contains("m", analysis.FreeVariables);
        Assert.Contains("b", analysis.FreeVariables);
    }

    [Fact]
    public void Column_name_can_be_replaced_with_x()
    {
        var node = Parser.Parse("m*x_1 + b");

        var rewritten = ExpressionRewriter.ReplaceIdentifier(node, "x_1", new IdentifierNode("x"));
        var binary = Assert.IsType<BinaryNode>(rewritten);
        var product = Assert.IsType<BinaryNode>(binary.Left);

        Assert.Equal("x", Assert.IsType<IdentifierNode>(product.Right).Name);
    }
}

public class RegressionSolverTests
{
    [Fact]
    public void Linear_model_recovers_slope_and_intercept()
    {
        double[] target = [3, 5, 7, 9];
        double[] xs = [1, 2, 3, 4];

        var result = RegressionSolver.Solve(
            target,
            [0, 0],
            parameters =>
            {
                var (m, b) = (parameters[0], parameters[1]);
                return xs.Select(x => m * x + b).ToArray();
            });

        Assert.NotNull(result);
        Assert.True(result.Converged, $"未收敛，残差 {result.ResidualSumOfSquares}");
        Assert.Equal(2, result.Parameters[0], 6);
        Assert.Equal(1, result.Parameters[1], 6);
        Assert.Equal(0, result.ResidualSumOfSquares, 9);
    }

    [Fact]
    public void Quadratic_model_recovers_coefficients()
    {
        double[] target = [2, 5, 10];
        double[] xs = [1, 2, 3];

        var result = RegressionSolver.Solve(
            target,
            [1, 0],
            parameters =>
            {
                var (a, b) = (parameters[0], parameters[1]);
                return xs.Select(x => a * x * x + b).ToArray();
            });

        Assert.NotNull(result);
        Assert.Equal(1, result.Parameters[0], 5);
        Assert.Equal(1, result.Parameters[1], 5);
    }

    [Fact]
    public void Missing_samples_are_ignored()
    {
        double[] target = [3, double.NaN, 7, 9];
        double[] xs = [1, 2, 3, 4];

        var result = RegressionSolver.Solve(
            target,
            [0, 0],
            parameters =>
            {
                var (m, b) = (parameters[0], parameters[1]);
                return xs.Select(x => m * x + b).ToArray();
            });

        Assert.NotNull(result);
        Assert.Equal(2, result.Parameters[0], 6);
        Assert.Equal(1, result.Parameters[1], 6);
    }

    [Fact]
    public void Too_few_samples_return_null()
    {
        Assert.Null(RegressionSolver.Solve([3], [0, 0], _ => [0]));
    }
}
