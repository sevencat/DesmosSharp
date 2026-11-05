namespace DesmosSharp.Core.Parsing;

public enum TokenKind
{
    Number,
    Identifier,
    Plus,
    Minus,
    Star,
    Slash,
    Caret,
    Bang,
    LeftParen,
    RightParen,
    LeftBracket,
    RightBracket,
    LeftBrace,
    RightBrace,
    Bar,
    Comma,
    Colon,
    Equal,
    Less,
    LessOrEqual,
    Greater,
    GreaterOrEqual,
    Tilde,
    EndOfInput,
}

public readonly record struct Token(TokenKind Kind, string Text, int Position, double Number = 0);
