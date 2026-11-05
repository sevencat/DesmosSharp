namespace DesmosSharp.Graphing;

/// <summary>
/// 图形视口：当前可见的数学坐标范围，负责数学坐标与屏幕像素坐标的互相换算。
/// </summary>
public readonly record struct GraphViewport(double XMin, double XMax, double YMin, double YMax)
{
    public static GraphViewport Default { get; } = new(-10, 10, -10, 10);

    public double Width => XMax - XMin;

    public double Height => YMax - YMin;

    public bool IsValid =>
        XMax > XMin &&
        YMax > YMin &&
        double.IsFinite(Width) &&
        double.IsFinite(Height);

    /// <summary>数学坐标 -> 屏幕像素坐标（原点在左上角，y 轴向下）。</summary>
    public (double X, double Y) ToScreen(double x, double y, double pixelWidth, double pixelHeight)
        => ((x - XMin) / Width * pixelWidth, (YMax - y) / Height * pixelHeight);

    /// <summary>屏幕像素坐标 -> 数学坐标。</summary>
    public (double X, double Y) ToGraph(double pixelX, double pixelY, double pixelWidth, double pixelHeight)
        => (XMin + pixelX / pixelWidth * Width, YMax - pixelY / pixelHeight * Height);
}
