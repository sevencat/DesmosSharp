using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DesmosSharp.App.Localization;

namespace DesmosSharp.App.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private static readonly string[] Palette =
    [
        "#C74440", "#2D70B3", "#388C46", "#6042A6", "#000000",
    ];

    public MainWindowViewModel()
    {
        Expressions =
        [
            new ExpressionItemViewModel(1, "y=sin(x)", CreateBrush(0)),
            new ExpressionItemViewModel(2, "y=mx+b", CreateBrush(1)),
        ];
    }

    public LocalizationService Localization => LocalizationService.Current;

    public ObservableCollection<ExpressionItemViewModel> Expressions { get; }

    [RelayCommand]
    private void AddExpression()
    {
        Expressions.Add(new ExpressionItemViewModel(
            Expressions.Count + 1,
            string.Empty,
            CreateBrush(Expressions.Count)));

        Renumber();
    }

    [RelayCommand]
    private void RemoveExpression(ExpressionItemViewModel item)
    {
        if (Expressions.Remove(item))
        {
            Renumber();
        }
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
