using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace DesmosSharp.App.ViewModels;

/// <summary>
/// 表达式列表中的一行。P1/P3 阶段会接上解析器与错误状态。
/// </summary>
public partial class ExpressionItemViewModel : ObservableObject
{
    public ExpressionItemViewModel(int number, string latex, IBrush color)
    {
        Number = number;
        _latex = latex;
        Color = color;
    }

    [ObservableProperty]
    private int _number;

    [ObservableProperty]
    private string _latex;

    [ObservableProperty]
    private bool _isVisible = true;

    public IBrush Color { get; }
}
