using DesmosSharp.Graphing;

namespace DesmosSharp.Rendering;

/// <summary>一次绘制所需的全部数据（视口、像素尺寸与待绘制函数）。</summary>
public sealed class GraphRenderScene
{
    public GraphRenderScene(
        GraphViewport viewport,
        double width,
        double height,
        IReadOnlyList<PlottedFunction> functions)
    {
        Viewport = viewport;
        Width = width;
        Height = height;
        Functions = functions;
    }

    public GraphViewport Viewport { get; }

    public double Width { get; }

    public double Height { get; }

    public IReadOnlyList<PlottedFunction> Functions { get; }
}
