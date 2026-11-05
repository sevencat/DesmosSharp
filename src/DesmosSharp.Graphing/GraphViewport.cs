namespace DesmosSharp.Graphing;

/// <summary>
/// 图形视口：当前可见的数学坐标范围，负责数学坐标与屏幕像素坐标的互相换算。
/// </summary>
public readonly record struct GraphViewport(double XMin, double XMax, double YMin, double YMax)
{
    public static GraphViewport Default { get; } = new(-10, 10, -10, 10);

    public double Width => XMax - XMin;

    public double Height => YMax - YMin;

    public double CenterX => (XMin + XMax) / 2;

    public double CenterY => (YMin + YMax) / 2;

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

    /// <summary>以某个屏幕点为锚点缩放：锚点下的数学坐标保持不动。</summary>
    public GraphViewport ZoomAt(
        double pixelX,
        double pixelY,
        double factor,
        double pixelWidth,
        double pixelHeight)
    {
        if (pixelWidth <= 0 || pixelHeight <= 0 || factor <= 0 || !double.IsFinite(factor))
        {
            return this;
        }

        var (anchorX, anchorY) = ToGraph(pixelX, pixelY, pixelWidth, pixelHeight);

        return new GraphViewport(
            anchorX + (XMin - anchorX) * factor,
            anchorX + (XMax - anchorX) * factor,
            anchorY + (YMin - anchorY) * factor,
            anchorY + (YMax - anchorY) * factor);
    }

    /// <summary>按像素位移平移：拖动内容向右，视口向左移动。</summary>
    public GraphViewport PanByPixels(double deltaX, double deltaY, double pixelWidth, double pixelHeight)
    {
        if (pixelWidth <= 0 || pixelHeight <= 0)
        {
            return this;
        }

        var shiftX = -deltaX * Width / pixelWidth;
        var shiftY = deltaY * Height / pixelHeight;

        return new GraphViewport(XMin + shiftX, XMax + shiftX, YMin + shiftY, YMax + shiftY);
    }

    /// <summary>让 x/y 每像素代表相同的数学长度（Desmos 默认的等比坐标）。</summary>
    public GraphViewport WithSquareUnits(double pixelWidth, double pixelHeight)
    {
        if (pixelWidth <= 0 || pixelHeight <= 0)
        {
            return this;
        }

        var unitsPerPixel = Math.Max(Width / pixelWidth, Height / pixelHeight);
        if (!double.IsFinite(unitsPerPixel) || unitsPerPixel <= 0)
        {
            return this;
        }

        var halfWidth = unitsPerPixel * pixelWidth / 2;
        var halfHeight = unitsPerPixel * pixelHeight / 2;

        return new GraphViewport(
            CenterX - halfWidth,
            CenterX + halfWidth,
            CenterY - halfHeight,
            CenterY + halfHeight);
    }
}
