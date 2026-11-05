using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Media;
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

    private IReadOnlyList<PlottedFunction> _functions = [];

    public MainWindowViewModel()
    {
        Expressions =
        [
            new ExpressionItemViewModel(1, "y=sin(x)", CreateBrush(0)),
            new ExpressionItemViewModel(2, "y=0.2x^2-2", CreateBrush(1)),
        ];

        foreach (var expression in Expressions)
        {
            expression.PropertyChanged += OnExpressionChanged;
        }

        RebuildFunctions();
    }

    public LocalizationService Localization => LocalizationService.Current;

    public ObservableCollection<ExpressionItemViewModel> Expressions { get; }

    /// <summary>供画板绘制的已编译函数。</summary>
    public IReadOnlyList<PlottedFunction> Functions
    {
        get => _functions;
        private set => SetProperty(ref _functions, value);
    }

    [RelayCommand]
    private void AddExpression()
    {
        var item = new ExpressionItemViewModel(
            Expressions.Count + 1,
            string.Empty,
            CreateBrush(Expressions.Count));

        item.PropertyChanged += OnExpressionChanged;
        Expressions.Add(item);

        Renumber();
        RebuildFunctions();
    }

    [RelayCommand]
    private void RemoveExpression(ExpressionItemViewModel item)
    {
        if (!Expressions.Remove(item))
        {
            return;
        }

        item.PropertyChanged -= OnExpressionChanged;
        Renumber();
        RebuildFunctions();
    }

    private void OnExpressionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ExpressionItemViewModel.Latex) or nameof(ExpressionItemViewModel.IsVisible))
        {
            RebuildFunctions();
        }
    }

    private void RebuildFunctions()
    {
        var built = new List<PlottedFunction>();

        foreach (var expression in Expressions)
        {
            if (!expression.IsVisible || !PlotModelBuilder.TryBuild(expression.Latex, out var compiled))
            {
                continue;
            }

            built.Add(new PlottedFunction(compiled, PlotModelBuilder.ToArgb(expression.Color)));
        }

        Functions = built;
    }

    private void Renumber()
    {
        for (var i = 0; i < Expressions.Count; i++)
        {
            Expressions[i].Number = i + 1;
        }
    }

    private static IBrush CreateBrush(int index)
        => new SolidColorBrush(Color.Parse(Palette[index % Palette.Length]));
}
