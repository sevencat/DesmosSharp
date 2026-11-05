using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using DesmosSharp.App.ViewModels;

namespace DesmosSharp.App.Views;

public partial class MainWindow : Window
{
    private MainWindowViewModel? _viewModel;

    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);

        if (_viewModel is not null)
        {
            _viewModel.RowAdded -= OnRowAdded;
        }

        _viewModel = DataContext as MainWindowViewModel;

        if (_viewModel is not null)
        {
            _viewModel.RowAdded += OnRowAdded;
        }
    }

    private void OnExpressionKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || _viewModel is null)
        {
            return;
        }

        if (sender is Control { DataContext: ExpressionItemViewModel row })
        {
            _viewModel.AddExpressionAfter(row);
            e.Handled = true;
        }
    }

    private void OnRowAdded(object? sender, int index) =>
        Dispatcher.UIThread.Post(() => FocusRow(index), DispatcherPriority.Loaded);

    private void FocusRow(int index)
    {
        var list = this.FindControl<ItemsControl>("ExpressionsList");
        if (list is null || index < 0 || index >= list.ItemCount)
        {
            return;
        }

        var textBox = list.ContainerFromIndex(index)?
            .GetVisualDescendants()
            .OfType<TextBox>()
            .FirstOrDefault();

        textBox?.Focus();
    }
}
