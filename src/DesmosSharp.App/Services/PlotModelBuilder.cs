using Avalonia.Media;
using DesmosSharp.Core.Analysis;
using DesmosSharp.Core.Evaluation;
using DesmosSharp.Core.Parsing;

namespace DesmosSharp.App.Services;

/// <summary>把一行表达式文本转成可绘制的编译结果。</summary>
internal static class PlotModelBuilder
{
    public static bool TryBuild(string text, out CompiledExpression expression)
    {
        expression = null!;

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
                return false;
            }

            // y = f(x) 取右侧；裸表达式直接当作 f(x)
            var body = node is CompareNode compare ? compare.Right : node;
            expression = ExpressionCompiler.Compile(body);
            return true;
        }
        catch (ParseException)
        {
            return false;
        }
    }

    public static uint ToArgb(IBrush brush) =>
        brush is ISolidColorBrush solid ? solid.Color.ToUInt32() : 0xFF000000;
}
