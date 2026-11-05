using DesmosSharp.Core.Parsing;

namespace DesmosSharp.Core.Evaluation;

/// <summary>数值运算，包含 Desmos 风格的列表广播语义。</summary>
public static class ValueOperations
{
    public static Value Apply(BinaryOperator op, Value left, Value right)
    {
        if (left.Kind == ValueKind.List || right.Kind == ValueKind.List)
        {
            return Broadcast(left, right, (a, b) => Apply(op, a, b));
        }

        if (left.TryGetNumber(out var a) && right.TryGetNumber(out var b))
        {
            return Value.FromNumber(ApplyNumber(op, a, b));
        }

        return Value.Undefined;
    }

    public static Value Negate(Value value) => MapNumbers(value, n => -n);

    public static Value Compare(CompareOperator op, Value left, Value right)
    {
        if (left.Kind == ValueKind.List || right.Kind == ValueKind.List)
        {
            return Broadcast(left, right, (a, b) => Compare(op, a, b));
        }

        if (left.TryGetNumber(out var a) && right.TryGetNumber(out var b))
        {
            return Value.FromBoolean(op switch
            {
                CompareOperator.Equal => a.Equals(b),
                CompareOperator.Less => a < b,
                CompareOperator.LessOrEqual => a <= b,
                CompareOperator.Greater => a > b,
                CompareOperator.GreaterOrEqual => a >= b,
                _ => false,
            });
        }

        if (left.TryGetBoolean(out var lb) && right.TryGetBoolean(out var rb))
        {
            return op == CompareOperator.Equal
                ? Value.FromBoolean(lb == rb)
                : Value.Undefined;
        }

        return Value.Undefined;
    }

    /// <summary>
    /// 把两个值组合成点，支持列表广播：
    /// <c>(x_1, y_1)</c> 会得到与表格行数等长的点列表。
    /// </summary>
    public static Value CombinePoints(Value x, Value y)
    {
        if (x.Kind == ValueKind.List || y.Kind == ValueKind.List)
        {
            return Broadcast(x, y, static (a, b) => CombinePoints(a, b));
        }

        return x.TryGetNumber(out var xNumber) && y.TryGetNumber(out var yNumber)
            ? Value.FromPoint(xNumber, yNumber)
            : Value.Undefined;
    }

    public static Value Factorial(Value value)
    {
        if (value.Kind == ValueKind.List)
        {
            var items = value.Items!;
            var mapped = new Value[items.Count];
            for (var i = 0; i < items.Count; i++)
            {
                mapped[i] = Factorial(items[i]);
            }

            return Value.FromList(mapped);
        }

        if (!value.TryGetNumber(out var number))
        {
            return Value.Undefined;
        }

        if (number < 0 || number > 170 || number != Math.Floor(number))
        {
            return Value.Undefined;
        }

        var result = 1d;
        for (var i = 2; i <= (int)number; i++)
        {
            result *= i;
        }

        return Value.FromNumber(result);
    }

    public static Value MapNumbers(Value value, Func<double, double> transform)
    {
        if (value.Kind == ValueKind.List)
        {
            var items = value.Items!;
            var mapped = new Value[items.Count];
            for (var i = 0; i < items.Count; i++)
            {
                mapped[i] = MapNumbers(items[i], transform);
            }

            return Value.FromList(mapped);
        }

        return value.TryGetNumber(out var number)
            ? Value.FromNumber(transform(number))
            : Value.Undefined;
    }

    private static double ApplyNumber(BinaryOperator op, double left, double right) => op switch
    {
        BinaryOperator.Add => left + right,
        BinaryOperator.Subtract => left - right,
        BinaryOperator.Multiply => left * right,
        BinaryOperator.Divide => left / right,
        BinaryOperator.Power => Math.Pow(left, right),
        _ => double.NaN,
    };

    private static Value Broadcast(Value left, Value right, Func<Value, Value, Value> operation)
    {
        if (left.Kind == ValueKind.List && right.Kind == ValueKind.List)
        {
            var leftItems = left.Items!;
            var rightItems = right.Items!;
            if (leftItems.Count != rightItems.Count)
            {
                return Value.Undefined;
            }

            var result = new Value[leftItems.Count];
            for (var i = 0; i < result.Length; i++)
            {
                result[i] = operation(leftItems[i], rightItems[i]);
            }

            return Value.FromList(result);
        }

        if (left.Kind == ValueKind.List)
        {
            var items = left.Items!;
            var result = new Value[items.Count];
            for (var i = 0; i < result.Length; i++)
            {
                result[i] = operation(items[i], right);
            }

            return Value.FromList(result);
        }

        var rightList = right.Items!;
        var mapped = new Value[rightList.Count];
        for (var i = 0; i < mapped.Length; i++)
        {
            mapped[i] = operation(left, rightList[i]);
        }

        return Value.FromList(mapped);
    }
}
