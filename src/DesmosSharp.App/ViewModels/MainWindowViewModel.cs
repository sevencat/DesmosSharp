using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DesmosSharp.App.Localization;
using DesmosSharp.App.Services;
using DesmosSharp.Core.Analysis;
using DesmosSharp.Core.Evaluation;
using DesmosSharp.Core.Parsing;
using DesmosSharp.Graphing;

namespace DesmosSharp.App.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private static readonly string[] Palette =
    [
        "#C74440", "#2D70B3", "#388C46", "#6042A6", "#000000",
    ];

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<Value>> EmptyColumns =
        new Dictionary<string, IReadOnlyList<Value>>();

    private readonly UndoManager _undoManager = new();
    private readonly Dictionary<string, ParameterViewModel> _parameters = new(StringComparer.Ordinal);
    private readonly Dictionary<string, int> _columnCounters = new(StringComparer.Ordinal);
    private readonly DispatcherTimer _animationTimer;

    private bool _isRestoring;
    private bool _suppressCurveRebuild;
    private IReadOnlyList<PlotItem> _plots = [];
    private int _tableCount;

    public MainWindowViewModel()
    {
        Expressions = [];

        _animationTimer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(33),
            DispatcherPriority.Background,
            (_, _) => OnAnimationTick());

        _animationTimer.Stop();

        AddRowInternal("y=sin(x)", CreateBrush(0));
        AddRowInternal("y=0.2x^2-2", CreateBrush(1));
        Renumber();
        RebuildGraph();
    }

    /// <summary>新增行后通知界面聚焦到该行。</summary>
    public event EventHandler<int>? RowAdded;

    public LocalizationService Localization => LocalizationService.Current;

    public ObservableCollection<ExpressionItemViewModel> Expressions { get; }

    /// <summary>数据表列表，列名可直接被表达式引用。</summary>
    public ObservableCollection<DataTableViewModel> Tables { get; } = [];

    /// <summary>所有表格列名，用于把列引用从滑块参数中排除。</summary>
    public IReadOnlyCollection<string> KnownColumns =>
        Tables.SelectMany(table => table.Columns).Select(column => column.Name).ToList();

    /// <summary>供画板绘制的图形元素。</summary>
    public IReadOnlyList<PlotItem> Plots
    {
        get => _plots;
        private set => SetProperty(ref _plots, value);
    }

    [RelayCommand]
    private void AddExpression() => AddExpressionAfter(Expressions.Count > 0 ? Expressions[^1] : null);

    [RelayCommand]
    private void AddTable()
    {
        var table = new DataTableViewModel(this, CreateBrush(_tableCount + 2));
        table.Initialize();
        Tables.Add(table);
        _tableCount++;
        RefreshAllRows();
        RebuildGraph();
    }

    public void RemoveTable(DataTableViewModel table)
    {
        if (!Tables.Remove(table))
        {
            return;
        }

        RefreshAllRows();
        RebuildGraph();
    }

    /// <summary>表格数据或结构变化后重新解析行并重绘。</summary>
    public void OnTableChanged()
    {
        RefreshAllRows();
        RebuildGraph();
    }

    /// <summary>列名增加或删除会影响行的自由变量，需要重新解析。</summary>
    private void RefreshAllRows()
    {
        foreach (var row in Expressions)
        {
            row.Refresh();
        }
    }

    /// <summary>分配不重复的表格列名，例如 <c>x_1</c>、<c>y_1</c>。</summary>
    public string NextColumnName(string prefix)
    {
        var index = _columnCounters.TryGetValue(prefix, out var current) ? current + 1 : 1;
        _columnCounters[prefix] = index;
        return $"{prefix}_{index}";
    }

    [RelayCommand]
    private void ClearAll()
    {
        if (Expressions.Count == 0)
        {
            return;
        }

        PushUndo();

        foreach (var row in Expressions)
        {
            Detach(row);
        }

        Expressions.Clear();
        RebuildGraph();
    }

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo()
    {
        var snapshot = _undoManager.Undo(Capture());
        if (snapshot is not null)
        {
            Restore(snapshot);
        }
    }

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo()
    {
        var snapshot = _undoManager.Redo(Capture());
        if (snapshot is not null)
        {
            Restore(snapshot);
        }
    }

    public void AddExpressionAfter(ExpressionItemViewModel? row)
    {
        PushUndo();

        var index = row is null ? Expressions.Count : Expressions.IndexOf(row) + 1;
        var item = CreateRow(string.Empty, CreateBrush(Expressions.Count));

        Expressions.Insert(index, item);
        Attach(item);
        Renumber();
        RebuildGraph();
        RowAdded?.Invoke(this, index);
    }

    public void DuplicateExpression(ExpressionItemViewModel row)
    {
        var index = Expressions.IndexOf(row);
        if (index < 0)
        {
            return;
        }

        PushUndo();

        var copy = CreateRow(row.Latex, row.Color, row.IsVisible);
        Expressions.Insert(index + 1, copy);
        Attach(copy);
        Renumber();
        RebuildGraph();
        RowAdded?.Invoke(this, index + 1);
    }

    public void RemoveExpression(ExpressionItemViewModel row)
    {
        if (!Expressions.Contains(row))
        {
            return;
        }

        PushUndo();

        Detach(row);
        Expressions.Remove(row);
        Renumber();
        RebuildGraph();
    }

    public void MoveExpression(ExpressionItemViewModel row, int offset)
    {
        var index = Expressions.IndexOf(row);
        var target = index + offset;

        if (index < 0 || target < 0 || target >= Expressions.Count)
        {
            return;
        }

        PushUndo();

        Expressions.Move(index, target);
        Renumber();
        RebuildGraph();
    }

    public void CycleColor(ExpressionItemViewModel row)
    {
        PushUndo();

        var next = (PaletteIndexOf(row.Color) + 1) % Palette.Length;
        row.Color = CreateBrushFromHex(Palette[next]);
        RebuildCurves();
    }

    private bool CanUndo() => _undoManager.CanUndo;

    private bool CanRedo() => _undoManager.CanRedo;

    private void PushUndo(string? mergeKey = null)
    {
        if (_isRestoring)
        {
            return;
        }

        _undoManager.Push(Capture(), mergeKey);

        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    private DocumentSnapshot Capture() => new(
        Expressions
            .Select(row => new ExpressionSnapshot(row.Latex, HexOf(row.Color), row.IsVisible))
            .ToList());

    private void Restore(DocumentSnapshot snapshot)
    {
        _isRestoring = true;

        try
        {
            foreach (var row in Expressions)
            {
                Detach(row);
            }

            Expressions.Clear();

            foreach (var item in snapshot.Expressions)
            {
                var row = CreateRow(item.Latex, CreateBrushFromHex(item.ColorHex), item.IsVisible);
                Expressions.Add(row);
                Attach(row);
            }

            Renumber();
        }
        finally
        {
            _isRestoring = false;
        }

        RebuildGraph();
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    private ExpressionItemViewModel CreateRow(string latex, IBrush color, bool isVisible = true)
        => new(this, latex, color, isVisible);

    private void AddRowInternal(string latex, IBrush color)
    {
        var row = CreateRow(latex, color);
        Expressions.Add(row);
        Attach(row);
    }

    private void Attach(ExpressionItemViewModel row)
    {
        row.PropertyChanging += OnRowPropertyChanging;
        row.PropertyChanged += OnRowPropertyChanged;
    }

    private void Detach(ExpressionItemViewModel row)
    {
        row.PropertyChanging -= OnRowPropertyChanging;
        row.PropertyChanged -= OnRowPropertyChanged;
    }

    private void OnRowPropertyChanging(object? sender, PropertyChangingEventArgs e)
    {
        if (_isRestoring || sender is not ExpressionItemViewModel row)
        {
            return;
        }

        if (e.PropertyName == nameof(ExpressionItemViewModel.Latex))
        {
            // 连续输入合并为一次撤销
            PushUndo($"text:{row.Id}");
        }
        else if (e.PropertyName == nameof(ExpressionItemViewModel.IsVisible))
        {
            PushUndo();
        }
    }

    private void OnRowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not ExpressionItemViewModel row)
        {
            return;
        }

        if (e.PropertyName == nameof(ExpressionItemViewModel.Latex))
        {
            row.Refresh();
        }

        if (e.PropertyName is nameof(ExpressionItemViewModel.Latex)
            or nameof(ExpressionItemViewModel.IsVisible)
            or nameof(ExpressionItemViewModel.ErrorMessage))
        {
            RebuildGraph();
        }
    }

    private void OnParameterChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ParameterViewModel.IsAnimating))
        {
            UpdateAnimationTimer();
            return;
        }

        if (_suppressCurveRebuild)
        {
            return;
        }

        if (e.PropertyName is nameof(ParameterViewModel.Value)
            or nameof(ParameterViewModel.Minimum)
            or nameof(ParameterViewModel.Maximum)
            or nameof(ParameterViewModel.Step))
        {
            RebuildCurves();
        }
    }

    /// <summary>重建滑块归属，并刷新曲线。</summary>
    private void RebuildGraph()
    {
        UpdateParameterOwnership();
        RebuildCurves();
        UpdateAnimationTimer();
    }

    private void UpdateParameterOwnership()
    {
        var used = new HashSet<string>(StringComparer.Ordinal);
        var owners = new Dictionary<string, ExpressionItemViewModel>(StringComparer.Ordinal);

        foreach (var row in Expressions)
        {
            row.Parameters.Clear();

            foreach (var name in row.ParameterNames)
            {
                used.Add(name);

                if (owners.ContainsKey(name))
                {
                    continue;
                }

                owners[name] = row;

                if (!_parameters.TryGetValue(name, out var parameter))
                {
                    parameter = new ParameterViewModel(name);
                    parameter.PropertyChanged += OnParameterChanged;
                    _parameters[name] = parameter;
                }

                row.Parameters.Add(parameter);
            }
        }

        foreach (var name in _parameters.Keys.Where(name => !used.Contains(name)).ToList())
        {
            _parameters[name].PropertyChanged -= OnParameterChanged;
            _parameters.Remove(name);
        }
    }

    private void RebuildCurves()
    {
        var built = new List<PlotItem>();
        var columns = CollectColumnValues();

        foreach (var row in Expressions)
        {
            if (!row.IsVisible)
            {
                continue;
            }

            if (row.Plot is RegressionPlot regression)
            {
                if (FitRegression(regression, columns, row) is { } curve)
                {
                    built.Add(curve with { ColorArgb = PlotModelBuilder.ToArgb(row.Color) });
                }

                continue;
            }

            if (row.Plot is not { } plot)
            {
                continue;
            }

            Dictionary<string, double>? values = null;

            if (row.ParameterNames.Count > 0)
            {
                values = new Dictionary<string, double>(StringComparer.Ordinal);

                foreach (var name in row.ParameterNames)
                {
                    if (_parameters.TryGetValue(name, out var parameter))
                    {
                        values[name] = parameter.Value;
                    }
                }
            }

            var bound = BindParameters(plot, values);
            built.Add((columns.Count == 0 ? bound : bound with { Columns = columns })
                with { ColorArgb = PlotModelBuilder.ToArgb(row.Color) });
        }

        foreach (var table in Tables)
        {
            var points = table.GetPoints();
            if (points.Count == 0)
            {
                continue;
            }

            built.Add(new ScatterPlot(points, PlotModelBuilder.ToArgb(table.Color), table.ShowLines));
        }

        Plots = built;
    }

    /// <summary>汇总所有表格列，作为表达式可直接引用的变量。</summary>
    private IReadOnlyDictionary<string, IReadOnlyList<Value>> CollectColumnValues()
    {
        if (Tables.Count == 0)
        {
            return EmptyColumns;
        }

        var result = new Dictionary<string, IReadOnlyList<Value>>(StringComparer.Ordinal);

        foreach (var table in Tables)
        {
            foreach (var (name, values) in table.GetColumnValues())
            {
                result[name] = values;
            }
        }

        return result;
    }

    /// <summary>用表格数据做最小二乘拟合，并把解换算成一条关于 x 的曲线。</summary>
    private ExplicitPlot? FitRegression(
        RegressionPlot regression,
        IReadOnlyDictionary<string, IReadOnlyList<Value>> columns,
        ExpressionItemViewModel row)
    {
        if (regression.ParameterNames.Count == 0 || columns.Count == 0)
        {
            return null;
        }

        var context = EvaluationContext.Create();

        foreach (var (name, values) in columns)
        {
            context.SetVariable(name, Value.FromList(values));
        }

        var target = ToSeries(regression.Target.Evaluate(context), 0);
        if (target.Length < regression.ParameterNames.Count)
        {
            return null;
        }

        var names = regression.ParameterNames;
        var model = ExpressionCompiler.Compile(regression.Model);
        var initial = new double[names.Count];

        for (var index = 0; index < names.Count; index++)
        {
            initial[index] = _parameters.TryGetValue(names[index], out var slider) ? slider.Value : 1;
        }

        double[] Predict(IReadOnlyList<double> parameters)
        {
            for (var index = 0; index < names.Count; index++)
            {
                context.SetNumber(names[index], parameters[index]);
            }

            return ToSeries(model.Evaluate(context), target.Length);
        }

        var result = RegressionSolver.Solve(target, initial, Predict);
        if (result is null)
        {
            return null;
        }

        row.RegressionSummary = string.Join(
            "   ",
            names.Select((name, index) => $"{name} = {result.Parameters[index]:G6}"));

        // 拟合曲线把模型里的自变量列换成 x，其余仍按解出的参数求值
        var curveNode = regression.XColumnName is not { } xColumn
            ? regression.Model
            : ExpressionRewriter.ReplaceIdentifier(regression.Model, xColumn, new IdentifierNode("x"));

        var curveParameters = new Dictionary<string, double>(StringComparer.Ordinal);

        for (var index = 0; index < names.Count; index++)
        {
            curveParameters[names[index]] = result.Parameters[index];
        }

        return new ExplicitPlot(ExpressionCompiler.Compile(curveNode), curveParameters, 0);
    }

    /// <summary>把求值结果转成与数据行对齐的数值序列，缺失处为 NaN。</summary>
    private static double[] ToSeries(Value value, int length)
    {
        if (value.TryGetNumber(out var number))
        {
            var broadcast = new double[length];
            Array.Fill(broadcast, number);
            return broadcast;
        }

        if (value.Kind != ValueKind.List)
        {
            return [];
        }

        var items = value.Items!;
        var series = new double[items.Count];

        for (var index = 0; index < series.Length; index++)
        {
            series[index] = items[index].TryGetNumber(out var item) ? item : double.NaN;
        }

        return series;
    }

    /// <summary>把滑块当前取值写入图形元素。</summary>
    private static PlotItem BindParameters(PlotItem plot, IReadOnlyDictionary<string, double>? values) => plot switch
    {
        ExplicitPlot item => item with { Parameters = values },
        ParametricPlot item => item with { Parameters = values },
        PolarPlot item => item with { Parameters = values },
        ImplicitPlot item => item with { Parameters = values },
        InequalityPlot item => item with { Parameters = values },
        VerticalLinePlot item => item with { Parameters = values },
        PointPlot item => item with { Parameters = values },
        _ => plot,
    };

    private void UpdateAnimationTimer()
    {
        if (_parameters.Values.Any(parameter => parameter.IsAnimating))
        {
            if (!_animationTimer.IsEnabled)
            {
                _animationTimer.Start();
            }
        }
        else
        {
            _animationTimer.Stop();
        }
    }

    private void OnAnimationTick()
    {
        var animating = false;

        _suppressCurveRebuild = true;

        try
        {
            foreach (var parameter in _parameters.Values)
            {
                if (!parameter.IsAnimating)
                {
                    continue;
                }

                animating = true;
                parameter.Advance();
            }
        }
        finally
        {
            _suppressCurveRebuild = false;
        }

        if (!animating)
        {
            _animationTimer.Stop();
            return;
        }

        RebuildCurves();
    }

    private void Renumber()
    {
        for (var i = 0; i < Expressions.Count; i++)
        {
            Expressions[i].Number = i + 1;
        }
    }

    private static IBrush CreateBrush(int index) => CreateBrushFromHex(Palette[index % Palette.Length]);

    private static IBrush CreateBrushFromHex(string hex) => new SolidColorBrush(Color.Parse(hex));

    private static string HexOf(IBrush brush) =>
        brush is ISolidColorBrush solid ? solid.Color.ToString() : "#FF000000";

    private static int PaletteIndexOf(IBrush brush)
    {
        if (brush is not ISolidColorBrush solid)
        {
            return -1;
        }

        var value = solid.Color.ToUInt32();

        for (var i = 0; i < Palette.Length; i++)
        {
            if (CreateBrushFromHex(Palette[i]) is ISolidColorBrush candidate &&
                candidate.Color.ToUInt32() == value)
            {
                return i;
            }
        }

        return -1;
    }
}
