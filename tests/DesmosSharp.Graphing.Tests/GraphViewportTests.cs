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

    [Fact]
    public void Zoom_keeps_the_anchor_point_fixed()
    {
        var viewport = GraphViewport.Default;
        var anchor = viewport.ToGraph(200, 150, 800, 600);

        var zoomed = viewport.ZoomAt(200, 150, 0.5, 800, 600);
        var anchorAfter = zoomed.ToGraph(200, 150, 800, 600);

        Assert.Equal(anchor.X, anchorAfter.X, 6);
        Assert.Equal(anchor.Y, anchorAfter.Y, 6);
        Assert.Equal(10, zoomed.Width, 6);
        Assert.Equal(10, zoomed.Height, 6);
    }

    [Fact]
    public void Panning_right_moves_the_viewport_left()
    {
        var panned = GraphViewport.Default.PanByPixels(100, 0, 800, 600);

        Assert.Equal(-12.5, panned.XMin, 6);
        Assert.Equal(7.5, panned.XMax, 6);
        Assert.Equal(-10, panned.YMin, 6);
    }

    [Fact]
    public void Dragging_down_moves_the_viewport_up()
    {
        var panned = GraphViewport.Default.PanByPixels(0, 60, 800, 600);

        Assert.Equal(-8, panned.YMin, 6);
        Assert.Equal(12, panned.YMax, 6);
    }

    [Fact]
    public void Square_units_equalize_both_axes()
    {
        var squared = new GraphViewport(-10, 10, -10, 10).WithSquareUnits(900, 600);

        var unitsPerPixelX = squared.Width / 900;
        var unitsPerPixelY = squared.Height / 600;

        Assert.Equal(unitsPerPixelX, unitsPerPixelY, 9);
        Assert.True(squared.Width >= 20);
        Assert.Equal(0, squared.CenterX, 9);
        Assert.Equal(0, squared.CenterY, 9);
    }
}
