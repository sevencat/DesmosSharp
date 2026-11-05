using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DesmosSharp.App.Services;
using DesmosSharp.Core.Evaluation;

namespace DesmosSharp.App.ViewModels;

/// <summary>表达式列表中的一行：文本、颜色、可见性、解析结果与所属滑块。</summary>
public partial class ExpressionItemViewModel : ObservableObject
{
    private readonly MainWindowViewModel _owner;

    public ExpressionItemViewModel(MainWindowViewModel owner, string latex, IBrush color, bool isVisible = true)
    {
        _owner = owner;
        _latex = latex;
        _color = color;
        _isVisible = isVisible;
        Refresh();
    }

    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>解析成功后的可绘制表达式；解析失败或非显函数时为 null。</summary>
    public CompiledExpression? Compiled { get; private set; }

    /// <summary>该行引用到的自由参数（不含自变量 x/y）。</summary>
    public IReadOnlyList<string> ParameterNames { get; private set; } = [];

    /// <summary>归属本行的滑块，由主视图模型计算。</summary>
    public ObservableCollection<ParameterViewModel> Parameters { get; } = [];

    [ObservableProperty]
    private int _number;

    [ObservableProperty]
    private string _latex;

    [ObservableProperty]
    private IBrush _color;

    [ObservableProperty]
    private bool _isVisible;

    [ObservableProperty]
    private string? _errorMessage;

    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);

    partial void OnErrorMessageChanged(string? value) => OnPropertyChanged(nameof(HasError));

    /// <summary>重新解析当前文本，更新 <see cref="Compiled"/> 与 <see cref="ErrorMessage"/>。</summary>
    public void Refresh()
    {
        if (string.IsNullOrWhiteSpace(Latex))
        {
            Compiled = null;
            ParameterNames = [];
            ErrorMessage = null;
            return;
        }

        if (PlotModelBuilder.TryBuild(Latex, out var compiled, out var parameters, out var error))
        {
            Compiled = compiled;
            ParameterNames = parameters;
            ErrorMessage = null;
        }
        else
        {
            Compiled = null;
            ParameterNames = [];
            ErrorMessage = error;
        }
    }

    [RelayCommand]
    private void Duplicate() => _owner.DuplicateExpression(this);

    [RelayCommand]
    private void Remove() => _owner.RemoveExpression(this);

    [RelayCommand]
    private void MoveUp() => _owner.MoveExpression(this, -1);

    [RelayCommand]
    private void MoveDown() => _owner.MoveExpression(this, 1);

    [RelayCommand]
    private void CycleColor() => _owner.CycleColor(this);
}
