using DesmosSharp.Core.Parsing;

namespace DesmosSharp.Core.Analysis;

/// <summary>表达式的图形类型，决定渲染器如何采样。</summary>
public enum ExpressionKind
{
    Empty,
    Number,
    ExplicitFunction,
    VerticalLine,
    ImplicitCurve,
    Inequality,
    PolarCurve,
    ParametricCurve,
    Point,
    PointList,
    FunctionDefinition,
}

public sealed class ExpressionAnalysis
{
    public required ExpressionKind Kind { get; init; }

    /// <summary>除赋值目标与函数参数之外的自由变量。</summary>
    public IReadOnlyList<string> FreeVariables { get; init; } = [];

    /// <summary>等号左侧的赋值目标或函数名。</summary>
    public string? AssignedVariable { get; init; }
}

/// <summary>根据语法树判断应如何绘制，并收集自由变量。</summary>
public static class ExpressionAnalyzer
{
    public static ExpressionAnalysis Analyze(
        ExpressionNode root,
        IReadOnlyCollection<string>? knownFunctions = null)
    {
        ArgumentNullException.ThrowIfNull(root);

        var variables = VariableCollector.Collect(root, knownFunctions);

        if (root is CompareNode compare)
        {
            return compare.Operator != CompareOperator.Equal
                ? Result(ExpressionKind.Inequality, variables)
                : AnalyzeEquation(compare, variables, knownFunctions);
        }

        if (root is PointNode point)
        {
            var pointVariables = VariableCollector.Collect(point, knownFunctions);
            return Result(
                pointVariables.Contains("t") ? ExpressionKind.ParametricCurve : ExpressionKind.Point,
                variables);
        }

        if (root is ListNode list && list.Items.Count > 0 && list.Items.All(item => item is PointNode))
        {
            return Result(ExpressionKind.PointList, variables);
        }

        if (root is NumberNode)
        {
            return Result(ExpressionKind.Number, variables);
        }

        return variables.Contains("x") || variables.Contains("y")
            ? Result(ExpressionKind.ExplicitFunction, variables, "y")
            : Result(ExpressionKind.Number, variables);
    }

    private static ExpressionAnalysis AnalyzeEquation(
        CompareNode compare,
        List<string> variables,
        IReadOnlyCollection<string>? knownFunctions)
    {
        // f(x) = ...：函数定义
        if (compare.Left is CallNode signature)
        {
            variables.Remove(signature.Name);

            foreach (var parameter in signature.Arguments.OfType<IdentifierNode>())
            {
                variables.Remove(parameter.Name);
            }

            return Result(ExpressionKind.FunctionDefinition, variables, signature.Name);
        }

        if (compare.Left is not IdentifierNode target)
        {
            return Result(ExpressionKind.ImplicitCurve, variables);
        }

        variables.Remove(target.Name);
        var rightVariables = VariableCollector.Collect(compare.Right, knownFunctions);

        return target.Name switch
        {
            // y = ...：右侧再出现 y 时不是显函数，而是隐函数（例如 y^2 = x）
            "y" => rightVariables.Contains("y")
                ? Result(ExpressionKind.ImplicitCurve, variables, "y")
                : Result(ExpressionKind.ExplicitFunction, variables, "y"),
            "r" => Result(ExpressionKind.PolarCurve, variables, "r"),
            "x" => rightVariables.Contains("y")
                ? Result(ExpressionKind.ImplicitCurve, variables, "x")
                : Result(ExpressionKind.VerticalLine, variables, "x"),
            _ => Result(ExpressionKind.Number, variables, target.Name),
        };
    }

    private static ExpressionAnalysis Result(
        ExpressionKind kind,
        List<string> variables,
        string? assignedVariable = null) => new()
        {
            Kind = kind,
            FreeVariables = variables,
            AssignedVariable = assignedVariable,
        };
}
