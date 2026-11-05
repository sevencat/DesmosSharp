using DesmosSharp.Graphing;

namespace DesmosSharp.Graphing.Tests;

public class GraphViewportTests
{
    [Fact]
    public void Default_viewport_is_valid()
    {
        Assert.True(GraphViewport.Default.IsValid);
        Assert.Equal(20, GraphViewport.Default.Width);
        Assert.Equal(20, GraphViewport.Default.Height);
    }

    [Fact]
    public void Origin_maps_to_screen_center()
    {
        var (sx, sy) = GraphViewport.Default.ToScreen(0, 0, 800, 600);

        Assert.Equal(400, sx, 6);
        Assert.Equal(300, sy, 6);
    }

    [Fact]
    public void Screen_to_graph_round_trips()
    {
        var viewport = new GraphViewport(-3.5, 7.25, -2, 4.75);

        var (gx, gy) = viewport.ToGraph(123, 456, 1000, 700);
        var (sx, sy) = viewport.ToScreen(gx, gy, 1000, 700);

        Assert.Equal(123, sx, 6);
        Assert.Equal(456, sy, 6);
    }

    [Fact]
    public void Degenerate_ranges_are_invalid()
    {
        Assert.False(new GraphViewport(1, 1, -1, 1).IsValid);
        Assert.False(new GraphViewport(-1, 1, 1, 0).IsValid);
    }
}
