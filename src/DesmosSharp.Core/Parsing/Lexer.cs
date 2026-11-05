using System.Globalization;
using System.Text;

namespace DesmosSharp.Core.Parsing;

/// <summary>把表达式文本切分为记号序列。</summary>
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

            if (TryReadIdentifier(text, ref index, out var name))
            {
                tokens.Add(new Token(TokenKind.Identifier, name, start));
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

    private static bool TryReadIdentifier(string text, ref int index, out string name)
    {
        if (index >= text.Length || !char.IsLetter(text[index]))
        {
            name = string.Empty;
            return false;
        }

        var builder = new StringBuilder();
        builder.Append(text[index]);
        index++;

        while (index < text.Length && char.IsLetterOrDigit(text[index]))
        {
            builder.Append(text[index]);
            index++;
        }

        // 下标：x_1、log_2、x_{12}
        if (index < text.Length && text[index] == '_')
        {
            builder.Append('_');
            index++;

            if (index < text.Length && text[index] == '{')
            {
                index++;
                while (index < text.Length && text[index] != '}')
                {
                    builder.Append(text[index]);
                    index++;
                }

                if (index < text.Length)
                {
                    index++; // 跳过 '}'
                }
            }
            else
            {
                while (index < text.Length && char.IsLetterOrDigit(text[index]))
                {
                    builder.Append(text[index]);
                    index++;
                }
            }
        }

        name = builder.ToString();
        return true;
    }
}
