using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia.Platform;

namespace DesmosSharp.App.Localization;

public sealed record LanguageOption(string Code, string DisplayName);

/// <summary>
/// 多语言资源服务。语言包以 JSON 存放在 <c>Assets/i18n/&lt;code&gt;.json</c>，
/// 切换语言时通过索引器绑定即时刷新界面。
/// </summary>
public sealed class LocalizationService : INotifyPropertyChanged
{
    private const string FallbackCode = "en";
    private const string AssetPathFormat = "avares://DesmosSharp.App/Assets/i18n/{0}.json";

    private static readonly LanguageOption[] SupportedLanguages =
    [
        new("zh-Hans", "中文"),
        new("en", "English"),
    ];

    private static readonly Lazy<LocalizationService> LazyInstance = new(() => new LocalizationService());

    private readonly Dictionary<string, Dictionary<string, string>> _catalogs;
    private LanguageOption _selectedLanguage;

    private LocalizationService()
    {
        _catalogs = LoadCatalogs();
        _selectedLanguage = Match(CultureInfo.CurrentUICulture);
    }

    public static LocalizationService Current => LazyInstance.Value;

    public event PropertyChangedEventHandler? PropertyChanged;

    public IReadOnlyList<LanguageOption> Languages => SupportedLanguages;

    public LanguageOption SelectedLanguage
    {
        get => _selectedLanguage;
        set
        {
            if (value is null || string.Equals(value.Code, _selectedLanguage.Code, StringComparison.Ordinal))
            {
                return;
            }

            _selectedLanguage = value;

            var culture = CultureInfo.GetCultureInfo(value.Code);
            CultureInfo.CurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedLanguage)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        }
    }

    public string this[string key] => Translate(key);

    public string Translate(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return string.Empty;
        }

        if (_catalogs.TryGetValue(_selectedLanguage.Code, out var catalog) &&
            catalog.TryGetValue(key, out var text))
        {
            return text;
        }

        if (_catalogs.TryGetValue(FallbackCode, out var fallback) &&
            fallback.TryGetValue(key, out var fallbackText))
        {
            return fallbackText;
        }

        return key;
    }

    private static Dictionary<string, Dictionary<string, string>> LoadCatalogs()
    {
        var catalogs = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var language in SupportedLanguages)
        {
            var uri = new Uri(string.Format(AssetPathFormat, language.Code));
            if (!AssetLoader.Exists(uri))
            {
                continue;
            }

            using var stream = AssetLoader.Open(uri);
            var catalog = JsonSerializer.Deserialize(stream, LocalizationJsonContext.Default.DictionaryStringString);

            if (catalog is not null)
            {
                catalogs[language.Code] = catalog;
            }
        }

        return catalogs;
    }

    private static LanguageOption Match(CultureInfo culture)
    {
        var exact = SupportedLanguages.FirstOrDefault(
            language => string.Equals(language.Code, culture.Name, StringComparison.OrdinalIgnoreCase));

        if (exact is not null)
        {
            return exact;
        }

        var prefix = culture.TwoLetterISOLanguageName;
        var byLanguage = SupportedLanguages.FirstOrDefault(
            language => language.Code.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

        return byLanguage ?? SupportedLanguages.First(language => language.Code == FallbackCode);
    }
}

[JsonSerializable(typeof(Dictionary<string, string>))]
internal sealed partial class LocalizationJsonContext : JsonSerializerContext
{
}
