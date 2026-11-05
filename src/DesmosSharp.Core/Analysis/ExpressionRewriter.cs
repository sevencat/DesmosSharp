using DesmosSharp.Core.Parsing;

namespace DesmosSharp.Core.Analysis;

/// <summary>语法树改写工具，例如把列名替换成自变量 <c>x</c>。</summary>
public static class ExpressionRewriter
{
    public static ExpressionNode ReplaceIdentifier(
        ExpressionNode node,
        string name,
        ExpressionNode replacement)
    {
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(replacement);

        return Rewrite(node);

        ExpressionNode Rewrite(ExpressionNode current) => current switch
        {
            IdentifierNode identifier when identifier.Name == name => replacement,

            UnaryNode unary => unary with { Operand = Rewrite(unary.Operand) },

            BinaryNode binary => binary with
            {
                Left = Rewrite(binary.Left),
                Right = Rewrite(binary.Right),
            },

            CompareNode compare => compare with
            {
                Left = Rewrite(compare.Left),
                Right = Rewrite(compare.Right),
            },

            FactorialNode factorial => factorial with { Operand = Rewrite(factorial.Operand) },

            ListNode list => list with
            {
                Items = list.Items.Select(Rewrite).ToArray(),
            },

            PointNode point => point with
            {
                X = Rewrite(point.X),
                Y = Rewrite(point.Y),
            },

            CallNode call => call with
            {
                Arguments = call.Arguments.Select(Rewrite).ToArray(),
            },

            RestrictedNode restricted => restricted with
            {
                Value = Rewrite(restricted.Value),
                Condition = Rewrite(restricted.Condition),
            },

            PiecewiseNode piecewise => piecewise with
            {
                Branches = piecewise.Branches
                    .Select(branch => new PiecewiseBranch(Rewrite(branch.Condition), Rewrite(branch.Value)))
                    .ToArray(),
            },

            RegressionNode regression => regression with
            {
                Target = Rewrite(regression.Target),
                Model = Rewrite(regression.Model),
            },

            _ => current,
        };
    }
}
