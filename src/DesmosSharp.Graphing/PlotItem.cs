using DesmosSharp.Core.Evaluation;
using DesmosSharp.Core.Parsing;

namespace DesmosSharp.Graphing;

/// <summary>一条待绘制图形元素的公共基类。</summary>
public abstract record PlotItem(uint ColorArgb);

/// <summary>显函数 <c>y = f(x)</c>。</summary>
public sealed record ExplicitPlot(
    CompiledExpression Expression,
    IReadOnlyDictionary<string, double>? Parameters,
    uint ColorArgb) : PlotItem(ColorArgb);

/// <summary>参数方程 <c>(x(t), y(t))</c>，求值结果是一个点。</summary>
public sealed record ParametricPlot(
    CompiledExpression PointExpression,
    double TMin,
    double TMax,
    IReadOnlyDictionary<string, double>? Parameters,
    uint ColorArgb) : PlotItem(ColorArgb);

/// <summary>极坐标 <c>r = f(θ)</c>。</summary>
public sealed record PolarPlot(
    CompiledExpression RadiusExpression,
    double ThetaMin,
    double ThetaMax,
    IReadOnlyDictionary<string, double>? Parameters,
    uint ColorArgb) : PlotItem(ColorArgb);

/// <summary>隐函数方程 <c>F(x, y) = 0</c>。</summary>
public sealed record ImplicitPlot(
    CompiledExpression Function,
    IReadOnlyDictionary<string, double>? Parameters,
    uint ColorArgb) : PlotItem(ColorArgb);

/// <summary>不等式：<see cref="Function"/> 为 <c>左式 - 右式</c>，按 <see cref="Operator"/> 判断符号。</summary>
public sealed record InequalityPlot(
    CompiledExpression Function,
    CompareOperator Operator,
    IReadOnlyDictionary<string, double>? Parameters,
    uint ColorArgb) : PlotItem(ColorArgb);

/// <summary>竖直线 <c>x = a</c>。</summary>
public sealed record VerticalLinePlot(
    CompiledExpression XExpression,
    IReadOnlyDictionary<string, double>? Parameters,
    uint ColorArgb) : PlotItem(ColorArgb);

/// <summary>点或点列（列表求值后得到多个点）。</summary>
public sealed record PointPlot(
    CompiledExpression Expression,
    IReadOnlyDictionary<string, double>? Parameters,
    uint ColorArgb) : PlotItem(ColorArgb);
