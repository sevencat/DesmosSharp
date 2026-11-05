using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Media;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DesmosSharp.App.Localization;
using DesmosSharp.App.Services;
using DesmosSharp.Graphing;

namespace DesmosSharp.App.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private static readonly string[] Palette =
    [
        "#C74440", "#2D70B3", "#388C46", "#6042A6", "#000000",
    ];

    private readonly UndoManager _undoManager = new();
    private readonly Dictionary<string, ParameterViewModel> _parameters = new(StringComparer.Ordinal);
    private readonly DispatcherTimer _animationTimer;

    private bool _isRestoring;
    private bool _suppressCurveRebuild;
    private IReadOnlyList<PlottedFunction> _functions = [];

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

    /// <summary>供画板绘制的已编译函数。</summary>
    public IReadOnlyList<PlottedFunction> Functions
    {
        get => _functions;
        private set => SetProperty(ref _functions, value);
    }

    [RelayCommand]
    private void AddExpression() => AddExpressionAfter(Expressions.Count > 0 ? Expressions[^1] : null);

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
        var built = new List<PlottedFunction>();

        foreach (var row in Expressions)
        {
            if (!row.IsVisible || row.Compiled is not { } compiled)
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

            built.Add(new PlottedFunction(compiled, PlotModelBuilder.ToArgb(row.Color), values));
        }

        Functions = built;
    }

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
