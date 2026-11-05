using DesmosSharp.Core.Parsing;

namespace DesmosSharp.Core.Evaluation;

/// <summary>把语法树编译为可重复求值的委托树。</summary>
public static class ExpressionCompiler
{
    public static CompiledExpression Compile(string text) => Compile(Parser.Parse(text));

    public static CompiledExpression Compile(ExpressionNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return new CompiledExpression(CompileNode(node));
    }

    /// <summary>从 <c>f(x) = x^2</c> 形式的定义创建用户函数。</summary>
    public static UserFunction CreateFunction(CallNode signature, ExpressionNode body)
    {
        var parameters = new List<string>(signature.Arguments.Count);
        foreach (var argument in signature.Arguments)
        {
            if (argument is not IdentifierNode identifier)
            {
                throw new ParseException("函数参数必须是变量名", 0);
            }

            parameters.Add(identifier.Name);
        }

        return new UserFunction(signature.Name, parameters, Compile(body));
    }

    private static Func<EvaluationContext, Value> CompileNode(ExpressionNode node) => node switch
    {
        NumberNode number => _ => Value.FromNumber(number.Value),
        IdentifierNode identifier => CompileIdentifier(identifier.Name),
        UnaryNode unary => CompileUnary(unary),
        BinaryNode binary => CompileBinary(binary),
        CompareNode compare => CompileCompare(compare),
        CallNode call => CompileCall(call),
        FactorialNode factorial => CompileFactorial(factorial),
        ListNode list => CompileList(list),
        PointNode point => CompilePoint(point),
        RestrictedNode restricted => CompileRestricted(restricted),
        PiecewiseNode piecewise => CompilePiecewise(piecewise),
        _ => _ => Value.Undefined,
    };

    private static Func<EvaluationContext, Value> CompileIdentifier(string name)
    {
        var constant = ResolveConstant(name);

        return constant.IsUndefined
            ? context => context.TryGetVariable(name, out var value) ? value : Value.Undefined
            : context => context.TryGetVariable(name, out var value) ? value : constant;
    }

    private static Func<EvaluationContext, Value> CompileUnary(UnaryNode unary)
    {
        var operand = CompileNode(unary.Operand);

        return unary.Operator switch
        {
            UnaryOperator.Negate => context => ValueOperations.Negate(operand(context)),
            _ => operand,
        };
    }

    private static Func<EvaluationContext, Value> CompileBinary(BinaryNode binary)
    {
        var left = CompileNode(binary.Left);
        var right = CompileNode(binary.Right);
        var op = binary.Operator;

        return context => ValueOperations.Apply(op, left(context), right(context));
    }

    private static Func<EvaluationContext, Value> CompileCompare(CompareNode compare)
    {
        var left = CompileNode(compare.Left);
        var right = CompileNode(compare.Right);
        var op = compare.Operator;

        return context => ValueOperations.Compare(op, left(context), right(context));
    }

    private static Func<EvaluationContext, Value> CompileCall(CallNode call)
    {
        var arguments = new Func<EvaluationContext, Value>[call.Arguments.Count];
        for (var i = 0; i < arguments.Length; i++)
        {
            arguments[i] = CompileNode(call.Arguments[i]);
        }

        if (BuiltinFunctions.TryResolve(call.Name, out var builtin))
        {
            return context => builtin(EvaluateArguments(arguments, context), context);
        }

        var name = call.Name;

        return context =>
        {
            var values = EvaluateArguments(arguments, context);

            if (context.TryGetFunction(name, out var userFunction))
            {
                return userFunction.Invoke(context, values);
            }

            // 未知函数名按隐式乘法处理：x(3) 等价于 x * 3
            if (values.Length == 1)
            {
                return ValueOperations.Apply(BinaryOperator.Multiply, ResolveName(name, context), values[0]);
            }

            return Value.Undefined;
        };
    }

    private static Func<EvaluationContext, Value> CompileFactorial(FactorialNode factorial)
    {
        var operand = CompileNode(factorial.Operand);
        return context => ValueOperations.Factorial(operand(context));
    }

    private static Func<EvaluationContext, Value> CompileList(ListNode list)
    {
        var items = new Func<EvaluationContext, Value>[list.Items.Count];
        for (var i = 0; i < items.Length; i++)
        {
            items[i] = CompileNode(list.Items[i]);
        }

        return context => Value.FromList(EvaluateArguments(items, context));
    }

    private static Func<EvaluationContext, Value> CompilePoint(PointNode point)
    {
        var x = CompileNode(point.X);
        var y = CompileNode(point.Y);

        return context =>
        {
            var xValue = x(context);
            var yValue = y(context);

            return xValue.TryGetNumber(out var xNumber) && yValue.TryGetNumber(out var yNumber)
                ? Value.FromPoint(xNumber, yNumber)
                : Value.Undefined;
        };
    }

    private static Func<EvaluationContext, Value> CompileRestricted(RestrictedNode restricted)
    {
        var value = CompileNode(restricted.Value);
        var condition = CompileNode(restricted.Condition);

        return context => condition(context).TryGetBoolean(out var allowed) && allowed
            ? value(context)
            : Value.Undefined;
    }

    private static Func<EvaluationContext, Value> CompilePiecewise(PiecewiseNode piecewise)
    {
        var branches = new (Func<EvaluationContext, Value> Condition, Func<EvaluationContext, Value> Value)[
            piecewise.Branches.Count];

        for (var i = 0; i < branches.Length; i++)
        {
            branches[i] = (CompileNode(piecewise.Branches[i].Condition), CompileNode(piecewise.Branches[i].Value));
        }

        return context =>
        {
            foreach (var (condition, value) in branches)
            {
                if (condition(context).TryGetBoolean(out var matched) && matched)
                {
                    return value(context);
                }
            }

            return Value.Undefined;
        };
    }

    private static Value[] EvaluateArguments(
        Func<EvaluationContext, Value>[] evaluators,
        EvaluationContext context)
    {
        var values = new Value[evaluators.Length];
        for (var i = 0; i < evaluators.Length; i++)
        {
            values[i] = evaluators[i](context);
        }

        return values;
    }

    private static Value ResolveName(string name, EvaluationContext context) =>
        context.TryGetVariable(name, out var value) ? value : ResolveConstant(name);

    private static Value ResolveConstant(string name) => name switch
    {
        "pi" => Value.FromNumber(Math.PI),
        "tau" => Value.FromNumber(Math.Tau),
        "e" => Value.FromNumber(Math.E),
        "infinity" => Value.FromNumber(double.PositiveInfinity),
        _ => Value.Undefined,
    };
}
