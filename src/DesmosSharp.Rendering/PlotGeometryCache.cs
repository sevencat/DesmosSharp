using DesmosSharp.Graphing;

namespace DesmosSharp.Rendering;

/// <summary>
/// 采样结果缓存：隐函数与不等式的网格求解较贵，
/// 相同图形 + 相同视口 + 相同像素尺寸时直接复用上一帧的几何数据。
/// </summary>
internal sealed class PlotGeometryCache
{
    private const int Capacity = 64;

    private readonly Dictionary<Key, PlotGeometry> _entries = [];
    private readonly Queue<Key> _order = new();

    public readonly record struct Key(PlotItem Plot, GraphViewport Viewport, double Width, double Height);

    public PlotGeometry GetOrAdd(Key key, Func<PlotGeometry> factory)
    {
        if (_entries.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var geometry = factory();

        while (_entries.Count >= Capacity && _order.Count > 0)
        {
            _entries.Remove(_order.Dequeue());
        }

        _entries[key] = geometry;
        _order.Enqueue(key);
        return geometry;
    }
}
