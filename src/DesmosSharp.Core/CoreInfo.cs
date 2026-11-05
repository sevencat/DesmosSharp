namespace DesmosSharp.Core;

/// <summary>内核基础信息，UI 与测试共用的最小锚点。</summary>
public static class CoreInfo
{
    public const string ProductName = "DesmosSharp";

    public static Version Version { get; } =
        typeof(CoreInfo).Assembly.GetName().Version ?? new Version(0, 0);
}
