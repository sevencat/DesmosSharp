using Avalonia.Data;
using Avalonia.Markup.Xaml;

namespace DesmosSharp.App.Localization;

/// <summary>XAML 用法：<c>Text="{i18n:Localize Expressions}"</c>。</summary>
public sealed class LocalizeExtension : MarkupExtension
{
    public LocalizeExtension()
    {
    }

    public LocalizeExtension(string key)
    {
        Key = key;
    }

    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider) =>
        new Binding($"[{Key}]")
        {
            Source = LocalizationService.Current,
            Mode = BindingMode.OneWay,
        };
}
