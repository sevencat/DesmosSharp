using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using DesmosSharp.App.ViewModels;
using DesmosSharp.App.Views;
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

        var plot = viewModel.Plots.OfType<ExplicitPlot>().Single(item => item.Parameters?.ContainsKey("m") == true);
        Assert.Equal(3, plot.Parameters!["m"]);

        var segments = FunctionSampler.Sample(
            plot.Expression,
            new GraphViewport(0, 2, -10, 10),
            2,
            plot.Parameters);

        var point = segments[0].Single(candidate => Math.Abs(candidate.X - 1) < 1e-9);
        Assert.Equal(3, point.Y, 9);
    }

    [AvaloniaFact]
    public void Removing_the_row_removes_its_slider()
    {
        var viewModel = new MainWindowViewModel();
        viewModel.Expressions[0].Latex = "y=mx";

        viewModel.RemoveExpression(viewModel.Expressions[0]);

        Assert.DoesNotContain(
            viewModel.Plots.OfType<ExplicitPlot>(),
            plot => plot.Parameters?.ContainsKey("m") == true);
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
    public void Settings_toggle_switches_the_range_editor()
    {
        var parameter = new ParameterViewModel("m");

        Assert.False(parameter.IsEditingSettings);

        parameter.ToggleSettingsCommand.Execute(null);
        Assert.True(parameter.IsEditingSettings);

        parameter.ToggleSettingsCommand.Execute(null);
        Assert.False(parameter.IsEditingSettings);
    }

    [AvaloniaFact]
    public void Slider_row_renders_value_and_range_editor()
    {
        var viewModel = new MainWindowViewModel();
        viewModel.Expressions[0].Latex = "y=mx";
        var parameter = viewModel.Expressions[0].Parameters.Single();
        parameter.Value = 2;
        parameter.IsEditingSettings = true;

        var window = new MainWindow { DataContext = viewModel };
        window.Show();

        var visible = window.GetVisualDescendants()
            .OfType<TextBox>()
            .Where(textBox => textBox.IsEffectivelyVisible)
            .Select(textBox => textBox.Text)
            .ToList();

        Assert.Contains("2", visible);
        Assert.Contains("-10", visible);
        Assert.Contains("10", visible);
        Assert.Contains("0.1", visible);
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
