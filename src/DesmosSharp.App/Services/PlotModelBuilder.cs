using Avalonia.Media;
using DesmosSharp.App.Localization;
using DesmosSharp.Core.Analysis;
using DesmosSharp.Core.Evaluation;
using DesmosSharp.Core.Parsing;
using DesmosSharp.Graphing;

namespace DesmosSharp.App.Services;

/// <summary>把一行表达式文本转成可绘制的图形元素，并给出行内错误信息与自由参数。</summary>
internal static class PlotModelBuilder
{
    /// <summary>绘图自变量与因变量，不作为滑块参数。</summary>
    private static readonly HashSet<string> ReservedVariables =
        new(StringComparer.Ordinal) { "x", "y", "t", "theta", "r" };

    public static bool TryBuild(
        string text,
        IReadOnlyCollection<string>? knownColumns,
        out PlotItem? plot,
        out IReadOnlyList<string> parameters,
        out string? error)
    {
        plot = null;
        parameters = [];
        error = null;

        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        try
        {
            var node = Parser.Parse(text);
            var analysis = ExpressionAnalyzer.Analyze(node);
            var built = BuildPlot(node, analysis, knownColumns, out var buildError);

            if (built is null)
            {
                // error 为 null 表示这类行合法但无需绘制（例如数字、函数定义）
                error = buildError;
                return false;
            }

            plot = built;

            // 表格列名是数据而不是滑块参数；回归参数由拟合结果决定，同样不生成滑块
            parameters = built is RegressionPlot
                ? []
                : analysis.FreeVariables
                    .Where(name => !ReservedVariables.Contains(name))
                    .Where(name => knownColumns?.Contains(name) != true)
                    .ToList();

            return true;
        }
        catch (ParseException exception)
        {
            error = exception.Message;
            return false;
        }
    }

    private static PlotItem? BuildPlot(
        ExpressionNode node,
        ExpressionAnalysis analysis,
        IReadOnlyCollection<string>? knownColumns,
        out string? error)
    {
        error = null;

        // y = ... / r = ... 等等式取右侧作为主体
        var body = node is CompareNode { Operator: CompareOperator.Equal } compare
            ? compare.Right
            : node;

        switch (analysis.Kind)
        {
            case ExpressionKind.ExplicitFunction:
                return new ExplicitPlot(ExpressionCompiler.Compile(body), null, 0);

            case ExpressionKind.VerticalLine:
                return new VerticalLinePlot(ExpressionCompiler.Compile(body), null, 0);

            case ExpressionKind.ParametricCurve:
                return new ParametricPlot(ExpressionCompiler.Compile(body), 0, Math.Tau, null, 0);

            case ExpressionKind.PolarCurve:
                return new PolarPlot(ExpressionCompiler.Compile(body), 0, Math.Tau, null, 0);

            case ExpressionKind.Point:
            case ExpressionKind.PointList:
                return new PointPlot(ExpressionCompiler.Compile(body), null, 0);

            case ExpressionKind.Regression when node is RegressionNode regression:
                return new RegressionPlot(
                    ExpressionCompiler.Compile(regression.Target),
                    regression.Model,
                    analysis.FreeVariables.FirstOrDefault(name =>
                        name.StartsWith("x_", StringComparison.Ordinal) &&
                        knownColumns?.Contains(name) == true),
                    analysis.FreeVariables
                        .Where(name => !ReservedVariables.Contains(name))
                        .Where(name => knownColumns?.Contains(name) != true)
                        .ToList(),
                    0);

            case ExpressionKind.ImplicitCurve when node is CompareNode implicitEquation:
                return new ImplicitPlot(ExpressionCompiler.Compile(Difference(implicitEquation)), null, 0);

            case ExpressionKind.Inequality when node is CompareNode inequality:
                return new InequalityPlot(
                    ExpressionCompiler.Compile(Difference(inequality)),
                    inequality.Operator,
                    null,
                    0);

            case ExpressionKind.Number:
            case ExpressionKind.FunctionDefinition:
                // 合法的纯数值行与函数定义行不产生图形，也不算错误
                return null;

            default:
                error = LocalizationService.Current["UnsupportedGraphKind"];
                return null;
        }
    }

    /// <summary>把等式/不等式左右两侧合成 <c>左式 - 右式</c>。</summary>
    private static ExpressionNode Difference(CompareNode comparison) =>
        new BinaryNode(BinaryOperator.Subtract, comparison.Left, comparison.Right);

    public static uint ToArgb(IBrush brush) =>
        brush is ISolidColorBrush solid ? solid.Color.ToUInt32() : 0xFF000000;
}
