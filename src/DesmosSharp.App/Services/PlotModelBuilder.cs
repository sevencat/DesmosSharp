using Avalonia.Media;
using DesmosSharp.App.Localization;
using DesmosSharp.Core.Analysis;
using DesmosSharp.Core.Evaluation;
using DesmosSharp.Core.Parsing;

namespace DesmosSharp.App.Services;

/// <summary>把一行表达式文本转成可绘制的编译结果，并给出行内错误信息与自由参数。</summary>
internal static class PlotModelBuilder
{
    /// <summary>绘图自变量，不作为滑块参数。</summary>
    private static readonly HashSet<string> ReservedVariables =
        new(StringComparer.Ordinal) { "x", "y" };

    public static bool TryBuild(
        string text,
        out CompiledExpression? expression,
        out IReadOnlyList<string> parameters,
        out string? error)
    {
        expression = null;
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

            if (analysis.Kind != ExpressionKind.ExplicitFunction)
            {
                error = LocalizationService.Current["UnsupportedGraphKind"];
                return false;
            }

            // y = f(x) 取右侧；裸表达式直接当作 f(x)
            var body = node is CompareNode compare ? compare.Right : node;
            expression = ExpressionCompiler.Compile(body);
            parameters = analysis.FreeVariables
                .Where(name => !ReservedVariables.Contains(name))
                .ToList();

            return true;
        }
        catch (ParseException exception)
        {
            error = exception.Message;
            return false;
        }
    }

    public static uint ToArgb(IBrush brush) =>
        brush is ISolidColorBrush solid ? solid.Color.ToUInt32() : 0xFF000000;
}
