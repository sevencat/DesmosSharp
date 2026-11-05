using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Skia;
using DesmosSharp.App.ViewModels;
using DesmosSharp.App.Views;

[assembly: AvaloniaTestApplication(typeof(DesmosSharp.App.HeadlessTests.TestAppBuilder))]

namespace DesmosSharp.App.HeadlessTests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<global::DesmosSharp.App.App>()
        .UseSkia()
        .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}

public class MainWindowTests
{
    [AvaloniaFact]
    public void Main_window_can_be_shown()
    {
        var window = new MainWindow { DataContext = new MainWindowViewModel() };

        window.Show();

        Assert.True(window.IsVisible);
    }

    [AvaloniaFact]
    public void Add_expression_appends_a_row()
    {
        var viewModel = new MainWindowViewModel();
        var before = viewModel.Expressions.Count;

        viewModel.AddExpressionCommand.Execute(null);

        Assert.Equal(before + 1, viewModel.Expressions.Count);
        Assert.Equal(before + 1, viewModel.Expressions[^1].Number);
    }

    [AvaloniaFact]
    public void Window_renders_a_frame()
    {
        var window = new MainWindow { DataContext = new MainWindowViewModel() };
        window.Show();

        var frame = window.CaptureRenderedFrame();

        Assert.NotNull(frame);
        Assert.True(frame.PixelSize.Width > 0);
        Assert.True(frame.PixelSize.Height > 0);
    }
}
