using System.Collections.ObjectModel;
using System.Globalization;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DesmosSharp.Core.Evaluation;
using DesmosSharp.Graphing;

namespace DesmosSharp.App.ViewModels;

/// <summary>表格中的一个可编辑单元格。</summary>
public sealed partial class TableCellViewModel : ObservableObject
{
    private readonly DataTableViewModel _owner;

    public TableCellViewModel(DataTableViewModel owner, string text = "")
    {
        _owner = owner;
        _text = text;
    }

    [ObservableProperty]
    private string _text;

    partial void OnTextChanged(string value) => _owner.NotifyChanged();
}

/// <summary>表格列，列名（<c>x_1</c>、<c>y_1</c>…）可直接在表达式里引用。</summary>
public sealed partial class TableColumnViewModel : ObservableObject
{
    private readonly DataTableViewModel _owner;

    public TableColumnViewModel(DataTableViewModel owner, string name)
    {
        _owner = owner;
        Name = name;
    }

    public string Name { get; }

    /// <summary>列前缀（x / y），用于把同组的 x、y 列配对成散点。</summary>
    public string Prefix => Name.Length > 0 && Name[0] is 'x' or 'y' ? Name[..1] : Name;

    /// <summary>列后缀（<c>x_1</c> 的 <c>1</c>）。</summary>
    public string Suffix => Name.IndexOf('_') is var index and >= 0 ? Name[(index + 1)..] : string.Empty;

    public ObservableCollection<TableCellViewModel> Cells { get; } = [];

    [RelayCommand]
    private void Remove() => _owner.RemoveColumn(this);
}

/// <summary>
/// 一张数据表：列名即变量名，表格自身在 x、y 列成对时绘制散点。
/// </summary>
public sealed partial class DataTableViewModel : ObservableObject
{
    private const int InitialRows = 3;

    private readonly MainWindowViewModel _owner;
    private int _rowCount = InitialRows;

    public DataTableViewModel(MainWindowViewModel owner, IBrush color)
    {
        _owner = owner;
        _color = color;
    }

    public ObservableCollection<TableColumnViewModel> Columns { get; } = [];

    [ObservableProperty]
    private IBrush _color;

    /// <summary>是否按行顺序把散点连成折线。</summary>
    [ObservableProperty]
    private bool _showLines;

    public int RowCount => _rowCount;

    partial void OnShowLinesChanged(bool value) => NotifyChanged();

    /// <summary>新建表格默认给一组 x / y 列。</summary>
    public void Initialize()
    {
        AddColumnCore("x");
        AddColumnCore("y");
    }

    public void NotifyChanged() => _owner.OnTableChanged();

    public IReadOnlyDictionary<string, IReadOnlyList<Value>> GetColumnValues()
    {
        var result = new Dictionary<string, IReadOnlyList<Value>>(StringComparer.Ordinal);

        foreach (var column in Columns)
        {
            var values = new Value[_rowCount];

            for (var row = 0; row < _rowCount; row++)
            {
                var text = row < column.Cells.Count ? column.Cells[row].Text : string.Empty;
                values[row] = TryParse(text, out var number) ? Value.FromNumber(number) : Value.Undefined;
            }

            result[column.Name] = values;
        }

        return result;
    }

    /// <summary>把成对的 x / y 列按行取值为散点。</summary>
    public IReadOnlyList<GraphPoint> GetPoints()
    {
        var points = new List<GraphPoint>();

        foreach (var xColumn in Columns.Where(column => column.Prefix == "x"))
        {
            var yColumn = Columns.FirstOrDefault(column =>
                column.Prefix == "y" && column.Suffix == xColumn.Suffix);

            if (yColumn is null)
            {
                continue;
            }

            var count = Math.Min(xColumn.Cells.Count, yColumn.Cells.Count);

            for (var row = 0; row < count; row++)
            {
                if (TryParse(xColumn.Cells[row].Text, out var x) &&
                    TryParse(yColumn.Cells[row].Text, out var y))
                {
                    points.Add(new GraphPoint(x, y));
                }
            }
        }

        return points;
    }

    public void RemoveColumn(TableColumnViewModel column)
    {
        if (!Columns.Remove(column))
        {
            return;
        }

        NotifyChanged();
    }

    [RelayCommand]
    private void AddRow()
    {
        _rowCount++;

        foreach (var column in Columns)
        {
            column.Cells.Add(new TableCellViewModel(this));
        }

        NotifyChanged();
    }

    [RelayCommand]
    private void RemoveRow()
    {
        if (_rowCount == 0)
        {
            return;
        }

        _rowCount--;

        foreach (var column in Columns)
        {
            if (column.Cells.Count > _rowCount)
            {
                column.Cells.RemoveAt(column.Cells.Count - 1);
            }
        }

        NotifyChanged();
    }

    [RelayCommand]
    private void AddXColumn()
    {
        AddColumnCore("x");
        NotifyChanged();
    }

    [RelayCommand]
    private void AddYColumn()
    {
        AddColumnCore("y");
        NotifyChanged();
    }

    [RelayCommand]
    private void RemoveTable() => _owner.RemoveTable(this);

    private void AddColumnCore(string prefix)
    {
        var column = new TableColumnViewModel(this, _owner.NextColumnName(prefix));

        for (var row = 0; row < _rowCount; row++)
        {
            column.Cells.Add(new TableCellViewModel(this));
        }

        Columns.Add(column);
    }

    private static bool TryParse(string text, out double value) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
}
