using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using DesmosSharp.App.Localization;
using DesmosSharp.App.ViewModels;
using DesmosSharp.App.Views;

namespace DesmosSharp.App.HeadlessTests;

public class LocalizationTests
{
    [AvaloniaFact]
    public void Switching_language_updates_bound_text()
    {
        var service = LocalizationService.Current;
        var window = new MainWindow { DataContext = new MainWindowViewModel() };
        window.Show();

        var header = window.FindControl<TextBlock>("ExpressionsHeader");
        Assert.NotNull(header);

        service.SelectedLanguage = service.Languages.Single(language => language.Code == "en");
        Assert.Equal("Expressions", header.Text);
        Assert.Equal("Ready", service.Translate("StatusReady"));

        service.SelectedLanguage = service.Languages.Single(language => language.Code == "zh-Hans");
        Assert.Equal("表达式", header.Text);
        Assert.Equal("就绪", service.Translate("StatusReady"));
    }

    [Fact]
    public void Missing_key_returns_the_key_itself()
    {
        Assert.Equal("NoSuchKey", LocalizationService.Current["NoSuchKey"]);
    }
}
