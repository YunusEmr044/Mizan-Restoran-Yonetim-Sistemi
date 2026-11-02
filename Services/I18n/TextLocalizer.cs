using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Localization;

namespace RestoranYonetim.Services.I18n;

// Müşteri tarafı arayüz metinleri. Anahtar Türkçe metnin kendisidir; diğer dillerin karşılıkları Resources/i18n/{dil}.json'dadır.
// Karşılığı olmayan metin Türkçe görünür.
public class TextLocalizer
{
    public const string ResourceFolder = "Resources/i18n";

    private static readonly Regex KeyPattern = new(@"\b_?[tT]\[""((?:[^""\\]|\\.)*)""", RegexOptions.Compiled);

    private readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> _dictionaries;

    public TextLocalizer(IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> dictionaries)
    {
        _dictionaries = dictionaries;
    }

    public static TextLocalizer FromFolder(string folder) =>
        new(SiteLanguages.All.Where(l => l != SiteLanguages.Default)
            .Select(l => (Lang: l, Path: Path.Combine(folder, l + ".json")))
            .Where(f => File.Exists(f.Path))
            .ToDictionary(f => f.Lang, f => (IReadOnlyDictionary<string, string>)Parse(File.ReadAllText(f.Path))));

    public static Dictionary<string, string> Parse(string json) =>
        JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new Dictionary<string, string>();

    public string this[string key] => Get(key, SiteLanguages.Current);

    public string this[string key, params object?[] args] => string.Format(CultureInfo.CurrentCulture, Get(key, SiteLanguages.Current), args);

    public string Get(string key, string lang) =>
        lang != SiteLanguages.Default && _dictionaries.TryGetValue(lang, out var dictionary) && dictionary.TryGetValue(key, out var value) && !string.IsNullOrEmpty(value)
            ? value
            : key;

    public static IEnumerable<string> KeysUsedIn(string source) =>
        KeyPattern.Matches(source).Select(m => Regex.Unescape(m.Groups[1].Value));
}

// DataAnnotations doğrulama ve görünen ad metinleri aynı sözlükten çevrilir.
public sealed class TextStringLocalizerFactory : IStringLocalizerFactory
{
    private readonly TextLocalizer _localizer;

    public TextStringLocalizerFactory(TextLocalizer localizer)
    {
        _localizer = localizer;
    }

    public IStringLocalizer Create(Type resourceSource) => new TextStringLocalizer(_localizer);

    public IStringLocalizer Create(string baseName, string location) => new TextStringLocalizer(_localizer);

    private sealed class TextStringLocalizer : IStringLocalizer
    {
        private readonly TextLocalizer _localizer;

        public TextStringLocalizer(TextLocalizer localizer)
        {
            _localizer = localizer;
        }

        public LocalizedString this[string name]
        {
            get
            {
                var value = _localizer[name];
                return new LocalizedString(name, value, resourceNotFound: value == name);
            }
        }

        public LocalizedString this[string name, params object[] arguments] =>
            new(name, string.Format(CultureInfo.CurrentCulture, _localizer[name], arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => Array.Empty<LocalizedString>();
    }
}
