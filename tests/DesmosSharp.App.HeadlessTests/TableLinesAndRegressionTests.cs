using Avalonia.Headless.XUnit;
using DesmosSharp.App.ViewModels;
using DesmosSharp.Graphing;

namespace DesmosSharp.App.HeadlessTests;

public class TableLinesTests
{
    [AvaloniaFact]
    public void Lines_toggle_connects_points_in_row_order()
    {
        var viewModel = new MainWindowViewModel();
        viewModel.AddTableCommand.Execute(null);

        var table = viewModel.Tables[0];
        var x = table.Columns.Single(column => column.Name == "x_1");
        var y = table.Columns.Single(column => column.Name == "y_1");

        table.AddRowCommand.Execute(null);

        string[] xs = ["0", "2", "4", "0"];
        string[] ys = ["0", "4", "0", "0"];

        for (var row = 0; row < xs.Length; row++)
        {
            x.Cells[row].Text = xs[row];
            y.Cells[row].Text = ys[row];
        }

        table.ShowLines = true;

        var scatter = Assert.Single(viewModel.Plots.OfType<ScatterPlot>());
        Assert.True(scatter.Connected);
        Assert.Equal(4, scatter.Points.Count);

        var geometry = PlotGeometryBuilder.Build(scatter, new GraphViewport(-5, 5, -5, 5), 400, 400);
        var polyline = Assert.Single(geometry.Polylines);

        Assert.Equal(4, polyline.Count);
        Assert.Equal(new GraphPoint(0, 0), polyline[0]);
        Assert.Equal(new GraphPoint(4, 0), polyline[2]);
        Assert.Equal(new GraphPoint(0, 0), polyline[3]);
    }

    [AvaloniaFact]
    public void Lines_off_keeps_points_separate()
    {
        var viewModel = new MainWindowViewModel();
        viewModel.AddTableCommand.Execute(null);

        var table = viewModel.Tables[0];
        table.Columns[0].Cells[0].Text = "1";
        table.Columns[1].Cells[0].Text = "2";

        var scatter = Assert.Single(viewModel.Plots.OfType<ScatterPlot>());

        Assert.False(scatter.Connected);
        Assert.Empty(PlotGeometryBuilder.Build(scatter, new GraphViewport(-5, 5, -5, 5), 200, 200).Polylines);
    }
}

public class RegressionRowTests
{
    private static (MainWindowViewModel ViewModel, ExpressionItemViewModel Row) CreateFit()
    {
        var viewModel = new MainWindowViewModel();
        viewModel.AddTableCommand.Execute(null);

        var table = viewModel.Tables[0];
        var x = table.Columns.Single(column => column.Name == "x_1");
        var y = table.Columns.Single(column => column.Name == "y_1");

        table.AddRowCommand.Execute(null);

        string[] xs = ["1", "2", "3", "4"];
        string[] ys = ["3", "5", "7", "9"];

        for (var row = 0; row < xs.Length; row++)
        {
            x.Cells[row].Text = xs[row];
            y.Cells[row].Text = ys[row];
        }

        var expression = viewModel.Expressions[0];
        expression.Latex = "y_1 ~ m*x_1 + b";

        return (viewModel, expression);
    }

    [AvaloniaFact]
    public void Regression_row_solves_parameters_without_sliders()
    {
        var (_, row) = CreateFit();

        Assert.IsType<RegressionPlot>(row.Plot);
        Assert.Empty(row.Parameters);
        Assert.Equal("m = 2   b = 1", row.RegressionSummary);
    }

    [AvaloniaFact]
    public void Regression_draws_the_fitted_line()
    {
        var (viewModel, _) = CreateFit();

        var curve = viewModel.Plots
            .OfType<ExplicitPlot>()
            .Single(plot => plot.Parameters?.ContainsKey("m") == true);

        var segments = FunctionSampler.Sample(
            curve.Expression,
            new GraphViewport(-1, 5, -1, 10),
            400,
            curve.Parameters);

        var points = segments.SelectMany(segment => segment).ToList();
        var nearest = points.OrderBy(point => Math.Abs(point.X - 4)).First();

        // 曲线应当就是 y = 2x + 1
        Assert.All(points, point => Assert.Equal(2 * point.X + 1, point.Y, 6));
        Assert.InRange(nearest.X, 3.95, 4.05);
        Assert.InRange(nearest.Y, 8.9, 9.1);
    }

    [AvaloniaFact]
    public void Data_change_refits_automatically()
    {
        var (viewModel, row) = CreateFit();
        var y = viewModel.Tables[0].Columns.Single(column => column.Name == "y_1");

        string[] updated = ["5", "8", "11", "14"];

        for (var index = 0; index < updated.Length; index++)
        {
            y.Cells[index].Text = updated[index];
        }

        Assert.Equal("m = 3   b = 2", row.RegressionSummary);
    }

    [AvaloniaFact]
    public void Regression_uses_the_column_named_in_the_model()
    {
        var viewModel = new MainWindowViewModel();

        // 第一张表与拟合无关，第二张才是数据源
        viewModel.AddTableCommand.Execute(null);
        viewModel.AddTableCommand.Execute(null);

        var fit = viewModel.Tables[1];
        fit.AddRowCommand.Execute(null);

        for (var index = 0; index < 4; index++)
        {
            fit.Columns[0].Cells[index].Text = (index + 1).ToString();
            fit.Columns[1].Cells[index].Text = (2 * (index + 1) + 1).ToString();
        }

        var row = viewModel.Expressions[0];
        row.Latex = "y_2 ~ m*x_2 + b";

        Assert.Equal("m = 2   b = 1", row.RegressionSummary);

        var curve = viewModel.Plots
            .OfType<ExplicitPlot>()
            .Single(plot => plot.Parameters?.ContainsKey("m") == true);

        var points = FunctionSampler
            .Sample(curve.Expression, new GraphViewport(0, 5, 0, 12), 200, curve.Parameters)
            .SelectMany(segment => segment)
            .ToList();

        Assert.All(points, point => Assert.Equal(2 * point.X + 1, point.Y, 6));
    }

    [AvaloniaFact]
    public void Missing_column_does_not_break_the_row()
    {
        var viewModel = new MainWindowViewModel();
        var row = viewModel.Expressions[0];

        row.Latex = "y_1 ~ m*x_1 + b";

        Assert.False(row.HasError);
        Assert.Null(row.RegressionSummary);
        Assert.Empty(row.Parameters);

        // 只剩第二行原本的显函数
        Assert.Single(viewModel.Plots.OfType<ExplicitPlot>());
    }
}
