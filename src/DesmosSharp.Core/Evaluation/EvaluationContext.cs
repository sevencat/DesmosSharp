namespace DesmosSharp.Core.Evaluation;

public enum AngleMode
{
    Radians,
    Degrees,
}

/// <summary>求值上下文：变量、函数定义与角度制设置。</summary>
public sealed class EvaluationContext
{
    private readonly Dictionary<string, Value> _variables;
    private readonly Dictionary<string, UserFunction> _functions;

    private EvaluationContext(EvaluationContext? parent)
    {
        _variables = new Dictionary<string, Value>(StringComparer.Ordinal);

        if (parent is null)
        {
            _functions = new Dictionary<string, UserFunction>(StringComparer.OrdinalIgnoreCase);
            AngleMode = AngleMode.Radians;
        }
        else
        {
            _functions = parent._functions;
            AngleMode = parent.AngleMode;
        }
    }

    public AngleMode AngleMode { get; set; }

    public static EvaluationContext Create() => new(null);

    /// <summary>创建子上下文，共享函数定义但拥有独立变量表（用于函数调用）。</summary>
    public EvaluationContext CreateChild() => new(this);

    public void SetVariable(string name, Value value) => _variables[name] = value;

    public void SetNumber(string name, double value) => SetVariable(name, Value.FromNumber(value));

    public bool TryGetVariable(string name, out Value value) => _variables.TryGetValue(name, out value);

    public void DefineFunction(UserFunction function) => _functions[function.Name] = function;

    public bool TryGetFunction(string name, out UserFunction function) =>
        _functions.TryGetValue(name, out function!);
}
