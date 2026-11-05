namespace DesmosSharp.Core.Evaluation;

/// <summary>编译后的表达式：一次编译，多次求值。</summary>
public sealed class CompiledExpression
{
    private readonly Func<EvaluationContext, Value> _evaluate;

    internal CompiledExpression(Func<EvaluationContext, Value> evaluate)
    {
        _evaluate = evaluate;
    }

    public Value Evaluate(EvaluationContext context) => _evaluate(context);

    /// <summary>以 x 为自变量求值，非数值结果返回 <see cref="double.NaN"/>。</summary>
    public double EvaluateAt(double x)
    {
        var context = EvaluationContext.Create();
        context.SetNumber("x", x);
        return Evaluate(context).TryGetNumber(out var value) ? value : double.NaN;
    }
}
