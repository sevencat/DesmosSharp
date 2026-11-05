using System.Globalization;
using DesmosSharp.Core.Evaluation;

namespace DesmosSharp.Core.Parsing;

/// <summary>
/// 把表达式文本切分为记号序列。
/// 与 Desmos 一致：连续的字母会被拆成「关键字 + 单字母变量」，例如 <c>ax</c> 是 <c>a*x</c>、<c>sinx</c> 是 <c>sin(x)</c>。
/// </summary>
public static class Lexer
{
    private static readonly Dictionary<char, string> UnicodeNames = new()
    {
        ['π'] = "pi",
        ['θ'] = "theta",
        ['τ'] = "tau",
        ['√'] = "sqrt",
        ['∑'] = "sum",
        ['∏'] = "prod",
        ['∞'] = "infinity",
    };

    /// <summary>可作为整体识别的保留名（函数与常量），按长度降序做最长匹配。</summary>
    private static readonly string[] Keywords = BuildKeywords();

    public static IReadOnlyList<Token> Tokenize(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var tokens = new List<Token>();
        var index = 0;

        while (index < text.Length)
        {
            var current = text[index];

            if (char.IsWhiteSpace(current))
            {
                index++;
                continue;
            }

            var start = index;

            if (char.IsDigit(current) ||
                (current == '.' && index + 1 < text.Length && char.IsDigit(text[index + 1])))
            {
                index++;
                while (index < text.Length && (char.IsDigit(text[index]) || text[index] == '.'))
                {
                    index++;
                }

                var raw = text[start..index];
                if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                {
                    throw new ParseException($"无法识别的数字 \"{raw}\"", start);
                }

                tokens.Add(new Token(TokenKind.Number, raw, start, number));
                continue;
            }

            if (UnicodeNames.TryGetValue(current, out var unicodeName))
            {
                index++;
                tokens.Add(new Token(TokenKind.Identifier, unicodeName, start));
                continue;
            }

            if (char.IsLetter(current))
            {
                ReadIdentifiers(text, ref index, tokens);
                continue;
            }

            // 双字符运算符
            if (index + 1 < text.Length)
            {
                var pair = text.Substring(index, 2);
                var pairKind = pair switch
                {
                    "<=" => TokenKind.LessOrEqual,
                    ">=" => TokenKind.GreaterOrEqual,
                    _ => (TokenKind?)null,
                };

                if (pairKind is { } kind)
                {
                    index += 2;
                    tokens.Add(new Token(kind, pair, start));
                    continue;
                }
            }

            var singleKind = current switch
            {
                '+' => TokenKind.Plus,
                '-' or '−' => TokenKind.Minus,
                '*' or '×' or '·' => TokenKind.Star,
                '/' or '÷' => TokenKind.Slash,
                '^' => TokenKind.Caret,
                '!' => TokenKind.Bang,
                '(' => TokenKind.LeftParen,
                ')' => TokenKind.RightParen,
                '[' => TokenKind.LeftBracket,
                ']' => TokenKind.RightBracket,
                '{' => TokenKind.LeftBrace,
                '}' => TokenKind.RightBrace,
                '|' => TokenKind.Bar,
                ',' => TokenKind.Comma,
                ':' => TokenKind.Colon,
                '=' => TokenKind.Equal,
                '<' or '≤' => TokenKind.Less,
                '>' or '≥' => TokenKind.Greater,
                '~' => TokenKind.Tilde,
                _ => (TokenKind?)null,
            };

            if (singleKind is not { } single)
            {
                throw new ParseException($"无法识别的字符 \"{current}\"", start);
            }

            index++;
            tokens.Add(new Token(single, current.ToString(), start));
        }

        tokens.Add(new Token(TokenKind.EndOfInput, string.Empty, text.Length));
        return tokens;
    }

    private static void ReadIdentifiers(string text, ref int index, List<Token> tokens)
    {
        var runStart = index;
        while (index < text.Length && char.IsLetterOrDigit(text[index]))
        {
            index++;
        }

        var run = text.AsSpan(runStart, index - runStart);
        var offset = 0;

        while (offset < run.Length)
        {
            string name;
            var keyword = MatchKeyword(run, offset);

            if (keyword is not null)
            {
                name = keyword;
                offset += keyword.Length;
            }
            else
            {
                name = run[offset].ToString();
                offset++;
            }

            // 紧随其后的数字视为下标：y1 -> y_1、log2 -> log_2
            var digitsStart = offset;
            while (offset < run.Length && char.IsDigit(run[offset]))
            {
                offset++;
            }

            if (offset > digitsStart)
            {
                name = $"{name}_{run[digitsStart..offset]}";
            }

            tokens.Add(new Token(TokenKind.Identifier, name, runStart));
        }

        // 显式下标：x_1、log_2、x_{12}
        if (index < text.Length && text[index] == '_' && tokens.Count > 0)
        {
            var last = tokens[^1];
            if (last.Kind != TokenKind.Identifier)
            {
                throw new ParseException("下标缺少主体", index);
            }

            index++;
            var subscript = ReadSubscript(text, ref index);
            if (subscript.Length == 0)
            {
                throw new ParseException("下标缺少内容", index);
            }

            tokens[^1] = last with { Text = $"{last.Text}_{subscript}" };
        }
    }

    private static string ReadSubscript(string text, ref int index)
    {
        if (index >= text.Length)
        {
            return string.Empty;
        }

        if (text[index] != '{')
        {
            var start = index;
            while (index < text.Length && (char.IsLetterOrDigit(text[index]) || text[index] == '.'))
            {
                index++;
            }

            return text[start..index];
        }

        index++; // 跳过 '{'
        var bodyStart = index;
        while (index < text.Length && text[index] != '}')
        {
            index++;
        }

        var body = text[bodyStart..index];
        if (index < text.Length)
        {
            index++; // 跳过 '}'
        }

        return body;
    }

    private static string? MatchKeyword(ReadOnlySpan<char> run, int offset)
    {
        var rest = run[offset..];

        foreach (var keyword in Keywords)
        {
            if (rest.StartsWith(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return keyword;
            }
        }

        return null;
    }

    private static string[] BuildKeywords()
    {
        // 关键字表直接取自内置函数表，避免两处维护造成漂移；
        // 其余为常量与暂未实现的运算符名（sum/prod 待 P5 支持）。
        var keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "pi", "tau", "theta", "infinity", "sum", "prod",
        };

        foreach (var name in BuiltinFunctions.Names)
        {
            if (name.Length >= 2)
            {
                keywords.Add(name);
            }
        }

        return keywords.OrderByDescending(keyword => keyword.Length).ToArray();
    }
}
