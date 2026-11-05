using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using DesmosSharp.App.ViewModels;
using DesmosSharp.App.Views;

namespace DesmosSharp.App.HeadlessTests;

public class ScreenshotProbe
{
    [AvaloniaFact]
    public void Save()
    {
        var viewModel = new MainWindowViewModel();
        viewModel.Expressions[0].Latex = "y=mx+b";
        viewModel.Expressions[0].Parameters[0].Value = 2;
        viewModel.Expressions[0].Parameters[0].IsEditingSettings = true;
        viewModel.Expressions[1].Latex = "y=sin(x)*a";
        viewModel.Expressions[1].Parameters[0].IsAnimating = true;

        var window = new MainWindow { DataContext = viewModel };
        window.Show();

        var frame = window.CaptureRenderedFrame();
        Assert.NotNull(frame);
        frame.Save(Path.Combine(AppContext.BaseDirectory, "sliders.png"));

        const double scale = 2;
        var bitmap = new RenderTargetBitmap(
            new PixelSize((int)(window.Bounds.Width * scale), (int)(window.Bounds.Height * scale)),
            new Vector(96 * scale, 96 * scale));
        bitmap.Render(window);
        bitmap.Save(Path.Combine(AppContext.BaseDirectory, "sliders@2x.png"));

        var lines = window.GetVisualDescendants()
            .OfType<TextBox>()
            .Select(textBox => $"Text='{textBox.Text}' Bounds={textBox.Bounds} FontSize={textBox.FontSize}");

        File.WriteAllLines(Path.Combine(AppContext.BaseDirectory, "diag.txt"), lines);
    }
}
