namespace DesmosSharp.Core.Evaluation;

/// <summary>用户自定义函数，例如 <c>f(x) = x^2</c>。</summary>
public sealed class UserFunction
{
    private readonly CompiledExpression _body;

    public UserFunction(string name, IReadOnlyList<string> parameters, CompiledExpression body)
    {
        Name = name;
        Parameters = parameters;
        _body = body;
    }

    public string Name { get; }

    public IReadOnlyList<string> Parameters { get; }

    public int Arity => Parameters.Count;

    public Value Invoke(EvaluationContext context, Value[] arguments)
    {
        if (arguments.Length != Parameters.Count)
        {
            return Value.Undefined;
        }

        var scope = context.CreateChild();
        for (var i = 0; i < arguments.Length; i++)
        {
            scope.SetVariable(Parameters[i], arguments[i]);
        }

        return _body.Evaluate(scope);
    }
}
