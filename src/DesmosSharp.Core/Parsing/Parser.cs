using DesmosSharp.Core.Evaluation;

namespace DesmosSharp.Core.Parsing;

/// <summary>
/// 递归下降 + 优先级爬升解析器。
/// 优先级由低到高：关系运算 → 加减（含定义域限制）→ 乘除/隐式乘法 → 一元正负 → 幂（右结合）→ 后缀 → 原子。
/// </summary>
public sealed class Parser
{
    private readonly IReadOnlyList<Token> _tokens;
    private int _index;
    private int _barDepth;

    private Parser(IReadOnlyList<Token> tokens)
    {
        _tokens = tokens;
    }

    public static ExpressionNode Parse(string text)
    {
        var parser = new Parser(Lexer.Tokenize(text));

        if (parser.Current.Kind == TokenKind.EndOfInput)
        {
            throw new ParseException("表达式为空", 0);
        }

        var first = parser.ParseExpression();

        // 顶层逗号分隔：点阵、多表达式
        if (parser.Current.Kind != TokenKind.Comma)
        {
            parser.Expect(TokenKind.EndOfInput);
            return first;
        }

        var items = new List<ExpressionNode> { first };
        while (parser.Match(TokenKind.Comma))
        {
            if (parser.Current.Kind == TokenKind.EndOfInput)
            {
                throw new ParseException("逗号后缺少表达式", parser.Current.Position);
            }

            items.Add(parser.ParseExpression());
        }

        parser.Expect(TokenKind.EndOfInput);
        return new ListNode(items);
    }

    private Token Current => _tokens[_index];

    private ExpressionNode ParseExpression() => ParseComparison();

    private ExpressionNode ParseComparison()
    {
        var left = ParseAdditive();

        var op = Current.Kind switch
        {
            TokenKind.Equal => CompareOperator.Equal,
            TokenKind.Less => CompareOperator.Less,
            TokenKind.LessOrEqual => CompareOperator.LessOrEqual,
            TokenKind.Greater => CompareOperator.Greater,
            TokenKind.GreaterOrEqual => CompareOperator.GreaterOrEqual,
            _ => (CompareOperator?)null,
        };

        if (op is not { } comparison)
        {
            return left;
        }

        _index++;
        var right = ParseAdditive();
        return new CompareNode(comparison, left, right);
    }

    private ExpressionNode ParseAdditive()
    {
        var left = ParseMultiplicative();

        while (true)
        {
            if (Match(TokenKind.Plus))
            {
                left = new BinaryNode(BinaryOperator.Add, left, ParseMultiplicative());
            }
            else if (Match(TokenKind.Minus))
            {
                left = new BinaryNode(BinaryOperator.Subtract, left, ParseMultiplicative());
            }
            else
            {
                break;
            }
        }

        // 定义域限制：|x|{x < 0}
        while (Match(TokenKind.LeftBrace))
        {
            var condition = ParseExpression();
            Expect(TokenKind.RightBrace);
            left = new RestrictedNode(left, condition);
        }

        return left;
    }

    private ExpressionNode ParseMultiplicative()
    {
        var left = ParseUnary();

        while (true)
        {
            if (Match(TokenKind.Star))
            {
                left = new BinaryNode(BinaryOperator.Multiply, left, ParseUnary());
            }
            else if (Match(TokenKind.Slash))
            {
                left = new BinaryNode(BinaryOperator.Divide, left, ParseUnary());
            }
            else if (StartsAtom(Current.Kind) && !(Current.Kind == TokenKind.Bar && _barDepth > 0))
            {
                // 隐式乘法：2x、2(x+1)、xy
                left = new BinaryNode(BinaryOperator.Multiply, left, ParseUnary());
            }
            else
            {
                break;
            }
        }

        return left;
    }

    private ExpressionNode ParseUnary()
    {
        if (Match(TokenKind.Minus))
        {
            return new UnaryNode(UnaryOperator.Negate, ParseUnary());
        }

        if (Match(TokenKind.Plus))
        {
            return new UnaryNode(UnaryOperator.Plus, ParseUnary());
        }

        return ParsePower();
    }

    private ExpressionNode ParsePower()
    {
        var left = ParsePostfix();

        if (Match(TokenKind.Caret))
        {
            return new BinaryNode(BinaryOperator.Power, left, ParseUnary());
        }

        return left;
    }

    private ExpressionNode ParsePostfix()
    {
        var node = ParsePrimary();

        while (Match(TokenKind.Bang))
        {
            node = new FactorialNode(node);
        }

        return node;
    }

    private ExpressionNode ParsePrimary()
    {
        var token = Current;

        switch (token.Kind)
        {
            case TokenKind.Number:
                _index++;
                return new NumberNode(token.Number);

            case TokenKind.Identifier:
                _index++;
                if (Current.Kind == TokenKind.LeftParen)
                {
                    return ParseCall(token.Text);
                }

                // 函数名后直接跟操作数：sin x、log_2 x、sqrt 4
                // 与 Desmos 一致，函数只吃下一个「幂级」操作数，因此 sin x^2 = sin(x^2)、sin x * 2 = sin(x)*2
                if (StartsAtom(Current.Kind) && BuiltinFunctions.TryResolve(token.Text, out _))
                {
                    return new CallNode(token.Text, [ParsePower()]);
                }

                return new IdentifierNode(token.Text);

            case TokenKind.LeftParen:
                return ParseParenthesized();

            case TokenKind.LeftBracket:
                return ParseList();

            case TokenKind.Bar:
                return ParseAbsoluteValue();

            case TokenKind.LeftBrace:
                return ParsePiecewise();

            default:
                throw new ParseException($"此处不应出现 \"{token.Text}\"", token.Position);
        }
    }

    private ExpressionNode ParseCall(string name)
    {
        Expect(TokenKind.LeftParen);

        var arguments = new List<ExpressionNode>();
        if (Current.Kind != TokenKind.RightParen)
        {
            arguments.Add(ParseExpression());
            while (Match(TokenKind.Comma))
            {
                arguments.Add(ParseExpression());
            }
        }

        Expect(TokenKind.RightParen);
        return new CallNode(name, arguments);
    }

    private ExpressionNode ParseParenthesized()
    {
        Expect(TokenKind.LeftParen);
        var first = ParseExpression();

        if (!Match(TokenKind.Comma))
        {
            Expect(TokenKind.RightParen);
            return first;
        }

        var second = ParseExpression();
        Expect(TokenKind.RightParen);
        return new PointNode(first, second);
    }

    private ExpressionNode ParseList()
    {
        Expect(TokenKind.LeftBracket);

        if (Current.Kind == TokenKind.RightBracket)
        {
            throw new ParseException("列表不能为空", Current.Position);
        }

        var items = new List<ExpressionNode> { ParseExpression() };
        while (Match(TokenKind.Comma))
        {
            items.Add(ParseExpression());
        }

        Expect(TokenKind.RightBracket);
        return new ListNode(items);
    }

    private ExpressionNode ParseAbsoluteValue()
    {
        Expect(TokenKind.Bar);
        _barDepth++;
        var inner = ParseExpression();
        _barDepth--;
        Expect(TokenKind.Bar);
        return new CallNode("abs", [inner]);
    }

    private ExpressionNode ParsePiecewise()
    {
        Expect(TokenKind.LeftBrace);
        var branches = new List<PiecewiseBranch>();

        while (true)
        {
            var condition = ParseExpression();
            if (!Match(TokenKind.Colon))
            {
                throw new ParseException("分段表达式缺少 \":\"", Current.Position);
            }

            var value = ParseExpression();
            branches.Add(new PiecewiseBranch(condition, value));

            if (!Match(TokenKind.Comma))
            {
                break;
            }
        }

        Expect(TokenKind.RightBrace);
        return new PiecewiseNode(branches);
    }

    private static bool StartsAtom(TokenKind kind) => kind is
        TokenKind.Number or
        TokenKind.Identifier or
        TokenKind.LeftParen or
        TokenKind.LeftBracket or
        TokenKind.Bar;

    private bool Match(TokenKind kind)
    {
        if (Current.Kind != kind)
        {
            return false;
        }

        _index++;
        return true;
    }

    private void Expect(TokenKind kind)
    {
        if (Current.Kind != kind)
        {
            throw new ParseException($"缺少 \"{kind}\"，实际为 \"{Current.Text}\"", Current.Position);
        }

        _index++;
    }
}
