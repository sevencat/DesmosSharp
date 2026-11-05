using Avalonia.Headless.XUnit;
using DesmosSharp.App.ViewModels;
using DesmosSharp.Graphing;

namespace DesmosSharp.App.HeadlessTests;

public class ExpressionListTests
{
    [AvaloniaFact]
    public void Initial_rows_produce_curves()
    {
        var viewModel = new MainWindowViewModel();

        Assert.Equal(2, viewModel.Expressions.Count);
        Assert.Equal(2, viewModel.Plots.Count);
    }

    [AvaloniaFact]
    public void Invalid_text_reports_an_error_and_draws_nothing()
    {
        var viewModel = new MainWindowViewModel();
        var row = viewModel.Expressions[0];

        row.Latex = "y=";

        Assert.True(row.HasError);
        Assert.NotNull(row.ErrorMessage);
        Assert.Null(row.Plot);
        Assert.Single(viewModel.Plots);
    }

    [AvaloniaFact]
    public void Implicit_equation_is_drawn()
    {
        var viewModel = new MainWindowViewModel();
        var row = viewModel.Expressions[0];

        row.Latex = "x^2+y^2=1";

        Assert.False(row.HasError);
        Assert.IsType<ImplicitPlot>(row.Plot);
        Assert.Equal(2, viewModel.Plots.Count);
    }

    [AvaloniaFact]
    public void Hiding_a_row_removes_its_curve()
    {
        var viewModel = new MainWindowViewModel();

        viewModel.Expressions[0].IsVisible = false;

        Assert.Single(viewModel.Plots);
    }

    [AvaloniaFact]
    public void Duplicate_inserts_right_below_the_source_row()
    {
        var viewModel = new MainWindowViewModel();

        viewModel.DuplicateExpression(viewModel.Expressions[0]);

        Assert.Equal(3, viewModel.Expressions.Count);
        Assert.Equal("y=sin(x)", viewModel.Expressions[1].Latex);
        Assert.Equal(2, viewModel.Expressions[1].Number);
        Assert.Equal(3, viewModel.Expressions[2].Number);
    }

    [AvaloniaFact]
    public void Move_reorders_and_renumbers()
    {
        var viewModel = new MainWindowViewModel();
        var first = viewModel.Expressions[0].Latex;

        viewModel.MoveExpression(viewModel.Expressions[0], 1);

        Assert.Equal(first, viewModel.Expressions[1].Latex);
        Assert.Equal(1, viewModel.Expressions[0].Number);
        Assert.Equal(2, viewModel.Expressions[1].Number);
    }

    [AvaloniaFact]
    public void Moving_past_the_edges_is_ignored()
    {
        var viewModel = new MainWindowViewModel();
        var first = viewModel.Expressions[0];

        viewModel.MoveExpression(first, -1);

        Assert.Same(first, viewModel.Expressions[0]);
    }

    [AvaloniaFact]
    public void Typing_is_undone_as_one_step()
    {
        var viewModel = new MainWindowViewModel();
        var row = viewModel.Expressions[0];

        row.Latex = "y=co";
        row.Latex = "y=cos(x)";

        viewModel.UndoCommand.Execute(null);

        Assert.Equal("y=sin(x)", viewModel.Expressions[0].Latex);
    }

    [AvaloniaFact]
    public void Redo_reapplies_the_edit()
    {
        var viewModel = new MainWindowViewModel();

        viewModel.Expressions[0].Latex = "y=cos(x)";
        viewModel.UndoCommand.Execute(null);
        viewModel.RedoCommand.Execute(null);

        Assert.Equal("y=cos(x)", viewModel.Expressions[0].Latex);
    }

    [AvaloniaFact]
    public void Delete_can_be_undone()
    {
        var viewModel = new MainWindowViewModel();

        viewModel.RemoveExpression(viewModel.Expressions[0]);
        Assert.Single(viewModel.Expressions);

        viewModel.UndoCommand.Execute(null);

        Assert.Equal(2, viewModel.Expressions.Count);
        Assert.Equal("y=sin(x)", viewModel.Expressions[0].Latex);
    }

    [AvaloniaFact]
    public void Clear_all_can_be_undone()
    {
        var viewModel = new MainWindowViewModel();

        viewModel.ClearAllCommand.Execute(null);
        Assert.Empty(viewModel.Expressions);
        Assert.Empty(viewModel.Plots);

        viewModel.UndoCommand.Execute(null);

        Assert.Equal(2, viewModel.Expressions.Count);
        Assert.Equal(2, viewModel.Plots.Count);
    }

    [AvaloniaFact]
    public void Color_cycles_through_the_palette()
    {
        var viewModel = new MainWindowViewModel();
        var row = viewModel.Expressions[0];
        var before = viewModel.Plots[0].ColorArgb;

        viewModel.CycleColor(row);

        Assert.NotEqual(before, viewModel.Plots[0].ColorArgb);
    }

    [AvaloniaFact]
    public void Add_expression_creates_an_empty_row_at_the_end()
    {
        var viewModel = new MainWindowViewModel();

        viewModel.AddExpressionCommand.Execute(null);

        Assert.Equal(3, viewModel.Expressions.Count);
        Assert.Equal(string.Empty, viewModel.Expressions[^1].Latex);
        Assert.False(viewModel.Expressions[^1].HasError);
    }
}
