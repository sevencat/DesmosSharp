using DesmosSharp.Core.Evaluation;

namespace DesmosSharp.Core.Tests;

public class ListBroadcastTests
{
    [Fact]
    public void Point_broadcasts_over_lists()
    {
        var expression = ExpressionCompiler.Compile("(x_1, y_1)");
        var context = EvaluationContext.Create();
        context.SetVariable("x_1", Value.FromList([Value.FromNumber(1), Value.FromNumber(2)]));
        context.SetVariable("y_1", Value.FromList([Value.FromNumber(3), Value.FromNumber(4)]));

        var value = expression.Evaluate(context);

        Assert.Equal(ValueKind.List, value.Kind);
        Assert.Equal(2, value.Items!.Count);

        Assert.True(value.Items[1].TryGetPoint(out var point));
        Assert.Equal(2, point.X);
        Assert.Equal(4, point.Y);
    }

    [Fact]
    public void Scalar_is_broadcast_against_a_list()
    {
        var expression = ExpressionCompiler.Compile("(1, y_1)");
        var context = EvaluationContext.Create();
        context.SetVariable("y_1", Value.FromList([Value.FromNumber(5), Value.FromNumber(6)]));

        var value = expression.Evaluate(context);

        Assert.Equal(2, value.Items!.Count);
        Assert.True(value.Items[0].TryGetPoint(out var first));
        Assert.Equal(1, first.X);
        Assert.Equal(5, first.Y);
    }

    [Fact]
    public void Long_lists_mismatch_is_undefined()
    {
        var expression = ExpressionCompiler.Compile("(x_1, y_1)");
        var context = EvaluationContext.Create();
        context.SetVariable("x_1", Value.FromList([Value.FromNumber(1), Value.FromNumber(2)]));
        context.SetVariable("y_1", Value.FromList([Value.FromNumber(3)]));

        Assert.True(expression.Evaluate(context).IsUndefined);
    }

    [Fact]
    public void Empty_cells_become_undefined_points()
    {
        var expression = ExpressionCompiler.Compile("(x_1, y_1)");
        var context = EvaluationContext.Create();
        context.SetVariable("x_1", Value.FromList([Value.FromNumber(1), Value.Undefined]));
        context.SetVariable("y_1", Value.FromList([Value.FromNumber(3), Value.FromNumber(4)]));

        var value = expression.Evaluate(context);

        Assert.Equal(2, value.Items!.Count);
        Assert.True(value.Items[0].Kind == ValueKind.Point);
        Assert.True(value.Items[1].IsUndefined);
    }
}
