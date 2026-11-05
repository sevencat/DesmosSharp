using DesmosSharp.Graphing;

namespace DesmosSharp.Graphing.Tests;

public class TickGeneratorTests
{
    [Fact]
    public void Default_viewport_produces_nice_steps()
    {
        var ticks = TickGenerator.Generate(GraphViewport.Default, 800, 600);

        var majors = ticks.X.Where(tick => tick.IsMajor).Select(tick => tick.Value).ToList();

        Assert.NotEmpty(majors);
        Assert.Contains(0, majors);
        Assert.Contains(4, majors);

        var step = majors[1] - majors[0];
        Assert.Contains(step, new[] { 1d, 2d, 5d });
    }

    [Fact]
    public void Major_ticks_carry_labels_and_minor_ticks_do_not()
    {
        var ticks = TickGenerator.Generate(GraphViewport.Default, 800, 600);

        Assert.All(ticks.X, tick => Assert.Equal(tick.IsMajor, tick.Label.Length > 0));
        Assert.Contains(ticks.X, tick => tick.Label == "4");
        Assert.All(ticks.X, tick => Assert.InRange(tick.Position, -10, 810));
    }

    [Fact]
    public void Pi_mode_labels_use_pi_fractions()
    {
        var ticks = TickGenerator.Generate(new GraphViewport(-Math.PI, Math.PI, -2, 2), 800, 600, usePiLabels: true);

        var labels = ticks.X.Where(tick => tick.IsMajor).Select(tick => tick.Label).ToList();

        Assert.Contains("π", labels);
        Assert.Contains("-π", labels);
    }

    [Fact]
    public void Pi_mode_produces_quarter_pi_subdivisions()
    {
        var ticks = TickGenerator.Generate(new GraphViewport(-4, 4, -3, 3), 800, 600, usePiLabels: true);

        var labels = ticks.X.Where(tick => tick.IsMajor).Select(tick => tick.Label).ToList();
        var piLabels = labels.Where(label => label != "0").ToList();

        Assert.Contains("π", labels);
        Assert.NotEmpty(piLabels);
        Assert.All(piLabels, label => Assert.Contains('π', label));
    }

    [Fact]
    public void Invalid_size_returns_empty_ticks()
    {
        var ticks = TickGenerator.Generate(GraphViewport.Default, 0, 0);

        Assert.Empty(ticks.X);
        Assert.Empty(ticks.Y);
    }
}
