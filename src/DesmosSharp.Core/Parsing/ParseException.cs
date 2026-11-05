namespace DesmosSharp.Core.Parsing;

/// <summary>解析失败。携带出错位置，供 UI 做行内提示。</summary>
public sealed class ParseException : Exception
{
    public ParseException(string message, int position)
        : base($"{message}（位置 {position}）")
    {
        Position = position;
    }

    public int Position { get; }
}
