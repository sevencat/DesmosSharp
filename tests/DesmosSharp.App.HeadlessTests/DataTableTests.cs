using Avalonia.Headless.XUnit;
using DesmosSharp.App.ViewModels;
using DesmosSharp.Graphing;

namespace DesmosSharp.App.HeadlessTests;

public class DataTableTests
{
    private static (MainWindowViewModel ViewModel, DataTableViewModel Table) CreateTableWithData()
    {
        var viewModel = new MainWindowViewModel();
        viewModel.AddTableCommand.Execute(null);

        var table = Assert.Single(viewModel.Tables);
        var x = table.Columns.Single(column => column.Name == "x_1");
        var y = table.Columns.Single(column => column.Name == "y_1");

        x.Cells[0].Text = "1";
        x.Cells[1].Text = "2";
        y.Cells[0].Text = "3";
        y.Cells[1].Text = "4";

        return (viewModel, table);
    }

    [AvaloniaFact]
    public void New_table_starts_with_an_x_and_y_column()
    {
        var viewModel = new MainWindowViewModel();

        viewModel.AddTableCommand.Execute(null);

        var table = Assert.Single(viewModel.Tables);
        Assert.Equal(["x_1", "y_1"], table.Columns.Select(column => column.Name));
        Assert.Equal(3, table.RowCount);
        Assert.All(table.Columns, column => Assert.Equal(3, column.Cells.Count));
    }

    [AvaloniaFact]
    public void Entered_data_is_drawn_as_scatter_points()
    {
        var (viewModel, _) = CreateTableWithData();

        var scatter = Assert.Single(viewModel.Plots.OfType<ScatterPlot>());

        Assert.Equal(2, scatter.Points.Count);
        Assert.Equal(new GraphPoint(1, 3), scatter.Points[0]);
        Assert.Equal(new GraphPoint(2, 4), scatter.Points[1]);
    }

    [AvaloniaFact]
    public void Empty_cells_are_skipped()
    {
        var (viewModel, table) = CreateTableWithData();
        table.Columns[1].Cells[1].Text = string.Empty;

        var scatter = Assert.Single(viewModel.Plots.OfType<ScatterPlot>());

        Assert.Single(scatter.Points);
    }

    [AvaloniaFact]
    public void Columns_can_be_referenced_by_expressions_without_sliders()
    {
        var (viewModel, _) = CreateTableWithData();
        var row = viewModel.Expressions[0];

        row.Latex = "(x_1, y_1)";

        Assert.IsType<PointPlot>(row.Plot);
        Assert.Empty(row.Parameters);

        var plot = viewModel.Plots.OfType<PointPlot>().Single();
        var points = PointSampler.Sample(plot.Expression, plot.Parameters, plot.Columns);

        Assert.Equal(2, points.Count);
        Assert.Equal(new GraphPoint(1, 3), points[0]);
    }

    [AvaloniaFact]
    public void Added_rows_keep_columns_aligned()
    {
        var (viewModel, table) = CreateTableWithData();

        table.AddRowCommand.Execute(null);

        Assert.Equal(4, table.RowCount);
        Assert.All(table.Columns, column => Assert.Equal(4, column.Cells.Count));

        table.RemoveRowCommand.Execute(null);
        table.RemoveRowCommand.Execute(null);

        Assert.Equal(2, table.RowCount);
        Assert.All(table.Columns, column => Assert.Equal(2, column.Cells.Count));
    }

    [AvaloniaFact]
    public void Removing_a_column_drops_its_scatter_series()
    {
        var (viewModel, table) = CreateTableWithData();
        var yColumn = table.Columns.Single(column => column.Name == "y_1");

        table.RemoveColumn(yColumn);

        Assert.Single(table.Columns);
        Assert.DoesNotContain(viewModel.Plots, plot => plot is ScatterPlot);
    }

    [AvaloniaFact]
    public void Extra_column_pairs_use_increasing_names()
    {
        var viewModel = new MainWindowViewModel();
        viewModel.AddTableCommand.Execute(null);

        var table = viewModel.Tables[0];
        table.AddXColumnCommand.Execute(null);
        table.AddYColumnCommand.Execute(null);

        Assert.Equal(["x_1", "y_1", "x_2", "y_2"], table.Columns.Select(column => column.Name));
    }

    [AvaloniaFact]
    public void Removing_a_table_removes_its_points()
    {
        var (viewModel, table) = CreateTableWithData();

        viewModel.RemoveTable(table);

        Assert.Empty(viewModel.Tables);
        Assert.DoesNotContain(viewModel.Plots, plot => plot is ScatterPlot);
    }
}
