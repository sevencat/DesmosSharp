using DesmosSharp.Core.Evaluation;
using DesmosSharp.Core.Parsing;

namespace DesmosSharp.Core.Analysis;

/// <summary>收集表达式中的自由变量（保持首次出现顺序）。</summary>
internal static class VariableCollector
{
    public static List<string> Collect(ExpressionNode node, IReadOnlyCollection<string>? knownFunctions = null)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        Walk(node, result, seen, knownFunctions);
        return result;
    }

    private static void Walk(
        ExpressionNode node,
        List<string> result,
        HashSet<string> seen,
        IReadOnlyCollection<string>? knownFunctions)
    {
        switch (node)
        {
            case NumberNode:
                break;

            case IdentifierNode identifier:
                Add(identifier.Name, result, seen);
                break;

            case UnaryNode unary:
                Walk(unary.Operand, result, seen, knownFunctions);
                break;

            case FactorialNode factorial:
                Walk(factorial.Operand, result, seen, knownFunctions);
                break;

            case BinaryNode binary:
                Walk(binary.Left, result, seen, knownFunctions);
                Walk(binary.Right, result, seen, knownFunctions);
                break;

            case CompareNode compare:
                Walk(compare.Left, result, seen, knownFunctions);
                Walk(compare.Right, result, seen, knownFunctions);
                break;

            case PointNode point:
                Walk(point.X, result, seen, knownFunctions);
                Walk(point.Y, result, seen, knownFunctions);
                break;

            case RestrictedNode restricted:
                Walk(restricted.Value, result, seen, knownFunctions);
                Walk(restricted.Condition, result, seen, knownFunctions);
                break;

            case ListNode list:
                foreach (var item in list.Items)
                {
                    Walk(item, result, seen, knownFunctions);
                }

                break;

            case PiecewiseNode piecewise:
                foreach (var branch in piecewise.Branches)
                {
                    Walk(branch.Condition, result, seen, knownFunctions);
                    Walk(branch.Value, result, seen, knownFunctions);
                }

                break;

            case CallNode call:
                // 未知函数名会被求值器当作隐式乘法，例如 x(3) => x * 3
                var isKnownFunction =
                    BuiltinFunctions.TryResolve(call.Name, out _) ||
                    (knownFunctions?.Contains(call.Name) ?? false);

                if (!isKnownFunction)
                {
                    Add(call.Name, result, seen);
                }

                foreach (var argument in call.Arguments)
                {
                    Walk(argument, result, seen, knownFunctions);
                }

                break;
        }
    }

    private static void Add(string name, List<string> result, HashSet<string> seen)
    {
        if (IsConstant(name))
        {
            return;
        }

        if (seen.Add(name))
        {
            result.Add(name);
        }
    }

    private static bool IsConstant(string name) =>
        name is "pi" or "tau" or "e" or "infinity";
}
