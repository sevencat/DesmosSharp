using Avalonia.Headless.XUnit;
using DesmosSharp.App.ViewModels;
using DesmosSharp.Graphing;

namespace DesmosSharp.App.HeadlessTests;

public class ParameterSliderTests
{
    [AvaloniaFact]
    public void Typing_free_variables_creates_sliders()
    {
        var viewModel = new MainWindowViewModel();

        viewModel.Expressions[0].Latex = "y=mx+b";

        Assert.Equal(["m", "b"], viewModel.Expressions[0].Parameters.Select(parameter => parameter.Name));
    }

    [AvaloniaFact]
    public void Independent_variable_is_not_a_slider()
    {
        var viewModel = new MainWindowViewModel();

        viewModel.Expressions[0].Latex = "y=sin(x)+k";

        var parameter = Assert.Single(viewModel.Expressions[0].Parameters);
        Assert.Equal("k", parameter.Name);
    }

    [AvaloniaFact]
    public void Slider_value_reaches_the_sampler()
    {
        var viewModel = new MainWindowViewModel();
        viewModel.Expressions[0].Latex = "y=mx";

        var slider = viewModel.Expressions[0].Parameters.Single();
        slider.Value = 3;

        var function = viewModel.Functions.Single(item => item.Parameters?.ContainsKey("m") == true);
        Assert.Equal(3, function.Parameters!["m"]);

        var segments = FunctionSampler.Sample(
            function.Expression,
            new GraphViewport(0, 2, -10, 10),
            2,
            function.Parameters);

        var point = segments[0].Single(candidate => Math.Abs(candidate.X - 1) < 1e-9);
        Assert.Equal(3, point.Y, 9);
    }

    [AvaloniaFact]
    public void Removing_the_row_removes_its_slider()
    {
        var viewModel = new MainWindowViewModel();
        viewModel.Expressions[0].Latex = "y=mx";

        viewModel.RemoveExpression(viewModel.Expressions[0]);

        Assert.DoesNotContain(viewModel.Functions, function => function.Parameters?.ContainsKey("m") == true);
    }

    [AvaloniaFact]
    public void Duplicated_row_reuses_the_same_slider()
    {
        var viewModel = new MainWindowViewModel();
        viewModel.Expressions[0].Latex = "y=mx";

        viewModel.DuplicateExpression(viewModel.Expressions[0]);

        Assert.Single(viewModel.Expressions[0].Parameters);
        Assert.Empty(viewModel.Expressions[1].Parameters);
        Assert.Equal(["m"], viewModel.Expressions[1].ParameterNames);
    }

    [AvaloniaFact]
    public void Value_is_clamped_into_the_range()
    {
        var parameter = new ParameterViewModel("m", 0, -1, 1);

        parameter.Value = 5;

        Assert.Equal(1, parameter.Value);
    }

    [AvaloniaFact]
    public void Animation_ping_pongs_between_bounds()
    {
        var parameter = new ParameterViewModel("m", 0, -1, 1, 0.5);

        parameter.Advance();
        Assert.Equal(0.5, parameter.Value, 9);

        parameter.Advance();
        Assert.Equal(1, parameter.Value, 9);

        parameter.Advance();
        Assert.Equal(0.5, parameter.Value, 9);
    }

    [AvaloniaFact]
    public void Changing_the_range_clamps_the_value()
    {
        var parameter = new ParameterViewModel("m", 0, -10, 10);

        parameter.Maximum = 2;
        parameter.Value = 9;

        Assert.Equal(2, parameter.Value);
    }

    [AvaloniaFact]
    public void Errors_do_not_create_sliders()
    {
        var viewModel = new MainWindowViewModel();

        viewModel.Expressions[0].Latex = "y=mx+";

        Assert.Empty(viewModel.Expressions[0].Parameters);
        Assert.True(viewModel.Expressions[0].HasError);
    }
}
