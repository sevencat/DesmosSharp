using System.Globalization;

namespace DesmosSharp.Core.Evaluation;

public delegate Value BuiltinFunction(Value[] args, EvaluationContext context);

/// <summary>内置函数表（对应手册「支持的函数」一页）。</summary>
public static class BuiltinFunctions
{
    private static readonly Dictionary<string, BuiltinFunction> Registry =
        new(StringComparer.OrdinalIgnoreCase);

    static BuiltinFunctions()
    {
        Add("abs", Unary(Math.Abs));
        Add("sqrt", Unary(Math.Sqrt));
        Add("cbrt", Unary(Math.Cbrt));
        Add("exp", Unary(Math.Exp));
        Add("ln", Unary(Math.Log));
        Add("log", Unary(Math.Log10));
        Add("sign", Unary(x => Math.Sign(x)));
        Add("ceil", Unary(Math.Ceiling));
        Add("floor", Unary(Math.Floor));
        Add("round", Unary(x => Math.Round(x, MidpointRounding.AwayFromZero)));
        Add("trunc", Unary(Math.Truncate));

        AddTrig("sin", Math.Sin);
        AddTrig("cos", Math.Cos);
        AddTrig("tan", Math.Tan);
        AddTrig("sec", x => 1 / Math.Cos(x));
        AddTrig("csc", x => 1 / Math.Sin(x));
        AddTrig("cot", x => 1 / Math.Tan(x));

        AddInverseTrig("arcsin", Math.Asin);
        AddInverseTrig("arccos", Math.Acos);
        AddInverseTrig("arctan", Math.Atan);
        AddInverseTrig("arcsec", x => Math.Acos(1 / x));
        AddInverseTrig("arccsc", x => Math.Asin(1 / x));
        AddInverseTrig("arccot", x => Math.PI / 2 - Math.Atan(x));

        Add("sinh", Unary(Math.Sinh));
        Add("cosh", Unary(Math.Cosh));
        Add("tanh", Unary(Math.Tanh));
        Add("sech", Unary(x => 1 / Math.Cosh(x)));
        Add("csch", Unary(x => 1 / Math.Sinh(x)));
        Add("coth", Unary(x => 1 / Math.Tanh(x)));

        Add("min", (args, _) => Reduce(args, Math.Min));
        Add("max", (args, _) => Reduce(args, Math.Max));
        Add("mod", Modulo);
        Add("gcd", GreatestCommonDivisor);
        Add("lcm", LeastCommonMultiple);
        Add("ncr", Combinations);
        Add("npr", Permutations);
    }

    /// <summary>全部内置函数名，供词法分析器识别保留字。</summary>
    public static IReadOnlyCollection<string> Names => Registry.Keys;

    public static bool TryResolve(string name, out BuiltinFunction function)
    {
        if (Registry.TryGetValue(name, out function!))
        {
            return true;
        }

        // 任意底对数：log_2(x)、log_{10}(x)
        if (name.StartsWith("log_", StringComparison.OrdinalIgnoreCase) &&
            double.TryParse(name[4..], NumberStyles.Float, CultureInfo.InvariantCulture, out var logBase) &&
            logBase > 0 &&
            logBase != 1)
        {
            function = (args, _) => args.Length == 1
                ? ValueOperations.MapNumbers(args[0], x => Math.Log(x) / Math.Log(logBase))
                : Value.Undefined;

            return true;
        }

        return false;
    }

    private static void Add(string name, BuiltinFunction function) => Registry[name] = function;

    private static BuiltinFunction Unary(Func<double, double> transform) =>
        (args, _) => args.Length == 1 ? ValueOperations.MapNumbers(args[0], transform) : Value.Undefined;

    private static void AddTrig(string name, Func<double, double> transform) =>
        Add(name, (args, context) => args.Length == 1
            ? ValueOperations.MapNumbers(args[0], x => transform(ToRadians(x, context)))
            : Value.Undefined);

    private static void AddInverseTrig(string name, Func<double, double> transform) =>
        Add(name, (args, context) => args.Length == 1
            ? ValueOperations.MapNumbers(args[0], x => FromRadians(transform(x), context))
            : Value.Undefined);

    private static double ToRadians(double value, EvaluationContext context) =>
        context.AngleMode == AngleMode.Degrees ? value * Math.PI / 180 : value;

    private static double FromRadians(double value, EvaluationContext context) =>
        context.AngleMode == AngleMode.Degrees ? value * 180 / Math.PI : value;

    private static Value Reduce(Value[] args, Func<double, double, double> combine)
    {
        if (args.Length == 0)
        {
            return Value.Undefined;
        }

        if (!args[0].TryGetNumber(out var result))
        {
            return Value.Undefined;
        }

        for (var i = 1; i < args.Length; i++)
        {
            if (!args[i].TryGetNumber(out var next))
            {
                return Value.Undefined;
            }

            result = combine(result, next);
        }

        return Value.FromNumber(result);
    }

    private static Value Modulo(Value[] args, EvaluationContext context)
    {
        if (args.Length != 2 ||
            !args[0].TryGetNumber(out var dividend) ||
            !args[1].TryGetNumber(out var divisor) ||
            divisor == 0)
        {
            return Value.Undefined;
        }

        return Value.FromNumber(dividend - divisor * Math.Floor(dividend / divisor));
    }

    private static Value GreatestCommonDivisor(Value[] args, EvaluationContext context)
    {
        if (!TryGetIntegers(args, out var left, out var right))
        {
            return Value.Undefined;
        }

        while (right != 0)
        {
            var remainder = left % right;
            left = right;
            right = remainder;
        }

        return Value.FromNumber(left);
    }

    private static Value LeastCommonMultiple(Value[] args, EvaluationContext context)
    {
        if (!TryGetIntegers(args, out var left, out var right))
        {
            return Value.Undefined;
        }

        if (left == 0 || right == 0)
        {
            return Value.FromNumber(0);
        }

        var a = left;
        var b = right;
        while (b != 0)
        {
            var remainder = a % b;
            a = b;
            b = remainder;
        }

        return Value.FromNumber(Math.Abs(left / a * right));
    }

    private static Value Combinations(Value[] args, EvaluationContext context)
    {
        if (args.Length != 2 ||
            !args[0].TryGetNumber(out var n) ||
            !args[1].TryGetNumber(out var r) ||
            n < 0 || r < 0 || n != Math.Floor(n) || r != Math.Floor(r) || r > n)
        {
            return Value.Undefined;
        }

        var result = 1d;
        for (var i = 0; i < (int)r; i++)
        {
            result = result * (n - i) / (i + 1);
        }

        return Value.FromNumber(Math.Round(result));
    }

    private static Value Permutations(Value[] args, EvaluationContext context)
    {
        if (args.Length != 2 ||
            !args[0].TryGetNumber(out var n) ||
            !args[1].TryGetNumber(out var r) ||
            n < 0 || r < 0 || n != Math.Floor(n) || r != Math.Floor(r) || r > n)
        {
            return Value.Undefined;
        }

        var result = 1d;
        for (var i = 0; i < (int)r; i++)
        {
            result *= n - i;
        }

        return Value.FromNumber(result);
    }

    private static bool TryGetIntegers(Value[] args, out long left, out long right)
    {
        left = 0;
        right = 0;

        if (args.Length != 2 ||
            !args[0].TryGetNumber(out var a) ||
            !args[1].TryGetNumber(out var b) ||
            a != Math.Floor(a) ||
            b != Math.Floor(b))
        {
            return false;
        }

        left = (long)Math.Abs(a);
        right = (long)Math.Abs(b);
        return true;
    }
}
