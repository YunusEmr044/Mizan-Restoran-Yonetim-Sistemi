using System.Text.RegularExpressions;

namespace RestoranYonetim.Models;

// Tema renk/font/köşe stili seçenekleri ve varsayılanları. Safe*() metotları geçersiz/boş
// değerleri varsayılana çevirir ki bozuk bir tema değeri siteyi bozmasın.
public static class ThemeCatalog
{
    // ---- Herkese açık site renkleri ----
    public const string DefaultPrimary = "#cd9f56";
    public const string DefaultPrimaryLight = "#f0cf94";
    public const string DefaultDark = "#100d0a";
    public const string DefaultBackground = "#faf6ef";
    public const string DefaultText = "#2a2117";

    // ---- Yönetim paneli renkleri ----
    public const string DefaultAdminAccent = "#cd9f56";
    public const string DefaultAdminSidebar = "#15120d";
    public const string DefaultAdminBackground = "#f4f2ec";

    public const string DefaultHeadingFont = "Playfair Display";
    public const string DefaultBodyFont = "Poppins";
    public const string DefaultCornerStyle = "rounded";

    // Sadece bu listede olan fontlar yüklenir; kullanıcı girdisiyle doğrudan URL oluşturulmaz.
    public static readonly Dictionary<string, (string Slug, int[] Weights)> HeadingFonts = new()
    {
        ["Playfair Display"] = ("playfair-display", new[] { 600, 700, 800 }),
        ["Cormorant Garamond"] = ("cormorant-garamond", new[] { 600, 700 }),
        ["Montserrat"] = ("montserrat", new[] { 600, 700, 800 }),
        ["Merriweather"] = ("merriweather", new[] { 700, 900 })
    };

    public static readonly Dictionary<string, (string Slug, int[] Weights)> BodyFonts = new()
    {
        ["Poppins"] = ("poppins", new[] { 300, 400, 500, 600, 700 }),
        ["Inter"] = ("inter", new[] { 300, 400, 500, 600, 700 }),
        ["Nunito Sans"] = ("nunito-sans", new[] { 300, 400, 600, 700 }),
        ["Lora"] = ("lora", new[] { 400, 500, 600, 700 })
    };

    private const string FontsourceRoot = "https://cdn.jsdelivr.net/npm/@fontsource/";
    private const string FontsourceVersion = "5.3.0";

    public static readonly IReadOnlyList<ThemePreset> Presets = new List<ThemePreset>
    {
        new("altin", "Koyu Altın", "Sıcak altın vurgu, koyu kahve zemin, klasik serif başlıklar.",
            DefaultPrimary, DefaultDark, DefaultBackground, DefaultText,
            DefaultHeadingFont, DefaultBodyFont, DefaultCornerStyle, DefaultAdminAccent, DefaultAdminSidebar, DefaultAdminBackground),
        new("bordo", "Bordo Şarap", "Gül bordo vurgu, koyu şarap zemin, zarif ince serif.",
            "#b5485d", "#1c0d12", "#fbf5f3", "#2b1a1e",
            "Cormorant Garamond", "Lora", "rounded", "#b5485d", "#1a0e12", "#f6f1f0"),
        new("zeytin", "Akdeniz Zeytin", "Terrakota vurgu, koyu zeytin zemin, sıcak ve samimi.",
            "#c0703f", "#1b2116", "#f7f4ec", "#26291f",
            "Merriweather", "Nunito Sans", "rounded", "#c0703f", "#191e14", "#f3f1ea"),
        new("gece", "Gece Mavisi", "Bakır vurgu, lacivert zemin, modern ve keskin hatlar.",
            "#c98a4b", "#0f1622", "#f5f6f8", "#1d2430",
            "Montserrat", "Inter", "sharp", "#c98a4b", "#0e141f", "#f1f3f6")
    };

    public static readonly Dictionary<string, string> CornerStyles = new()
    {
        ["rounded"] = "Yuvarlak (modern)",
        ["sharp"] = "Keskin (klasik)"
    };

    private static readonly Regex HexPattern = new("^#[0-9A-Fa-f]{6}$", RegexOptions.Compiled);

    public static string SafeColor(string? value, string fallback) =>
        !string.IsNullOrWhiteSpace(value) && HexPattern.IsMatch(value.Trim()) ? value.Trim() : fallback;

    public static string SafeHeadingFont(string? value) =>
        !string.IsNullOrWhiteSpace(value) && HeadingFonts.ContainsKey(value) ? value : DefaultHeadingFont;

    public static string SafeBodyFont(string? value) =>
        !string.IsNullOrWhiteSpace(value) && BodyFonts.ContainsKey(value) ? value : DefaultBodyFont;

    public static string SafeCornerStyle(string? value) =>
        !string.IsNullOrWhiteSpace(value) && CornerStyles.ContainsKey(value) ? value : DefaultCornerStyle;

    // "#rrggbb" -> "r, g, b" (rgba(var(--x-rgb), .2) gibi kullanımlar için).
    public static string HexToRgbTriplet(string hexColor)
    {
        var hex = hexColor.TrimStart('#');
        var r = Convert.ToInt32(hex.Substring(0, 2), 16);
        var g = Convert.ToInt32(hex.Substring(2, 2), 16);
        var b = Convert.ToInt32(hex.Substring(4, 2), 16);
        return $"{r}, {g}, {b}";
    }

    // Bir rengi beyaza doğru açar (amount: 0 = değişmez, 1 = beyaz).
    public static string Lighten(string hexColor, double amount)
    {
        var hex = hexColor.TrimStart('#');
        var r = Convert.ToInt32(hex.Substring(0, 2), 16);
        var g = Convert.ToInt32(hex.Substring(2, 2), 16);
        var b = Convert.ToInt32(hex.Substring(4, 2), 16);
        r += (int)((255 - r) * amount);
        g += (int)((255 - g) * amount);
        b += (int)((255 - b) * amount);
        return $"#{r:X2}{g:X2}{b:X2}";
    }

    public static void ApplyToViewBag(dynamic viewBag, SiteContent content)
    {
        var primary = SafeColor(content.ThemePrimaryColor, DefaultPrimary);
        var primaryLight = PrimaryLightFor(primary);
        var dark = SafeColor(content.ThemeDarkColor, DefaultDark);
        var background = SafeColor(content.ThemeBackgroundColor, DefaultBackground);
        var text = SafeColor(content.ThemeTextColor, DefaultText);
        var headingFont = SafeHeadingFont(content.ThemeHeadingFont);
        var bodyFont = SafeBodyFont(content.ThemeBodyFont);
        var cornerStyle = SafeCornerStyle(content.ThemeCornerStyle);

        viewBag.ThemePrimaryColor = primary;
        viewBag.ThemePrimaryColorRgb = HexToRgbTriplet(primary);
        viewBag.ThemePrimaryLightColor = primaryLight;
        viewBag.ThemeDarkColor = dark;
        viewBag.ThemeDarkColorRgb = HexToRgbTriplet(dark);
        viewBag.ThemeBackgroundColor = background;
        viewBag.ThemeTextColor = text;
        viewBag.ThemeHeadingFont = headingFont;
        viewBag.ThemeBodyFont = bodyFont;
        viewBag.ThemeButtonRadius = cornerStyle == "sharp" ? "10px" : "999px";
        viewBag.ThemeCardRadius = cornerStyle == "sharp" ? "6px" : "18px";
        viewBag.ThemeFontStylesheets = FontStylesheets(headingFont, bodyFont);
        viewBag.ThemeOnPrimary = ReadableTextOn(primary);
    }

    // Koyu zemin üzerindeki küçük vurgu metinleri bu açık tonu kullanır.
    public static string PrimaryLightFor(string primary) => Lighten(primary, 0.38);

    public static IReadOnlyList<string> FontStylesheets(string headingFont, string bodyFont) =>
        StylesheetsFor(SafeHeadingFont(headingFont)).Concat(StylesheetsFor(SafeBodyFont(bodyFont))).Distinct().ToList();

    public static IReadOnlyList<string> StylesheetsFor(string font) =>
        HeadingFonts.TryGetValue(font, out var f) || BodyFonts.TryGetValue(font, out f)
            ? f.Weights.Select(w => $"{FontsourceRoot}{f.Slug}@{FontsourceVersion}/{w}.css").ToList()
            : [];

    // WCAG 2.x kontrast oranı (1-21).
    public static double Contrast(string hexA, string hexB)
    {
        var a = RelativeLuminance(hexA);
        var b = RelativeLuminance(hexB);
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }

    public static string ReadableTextOn(string hex) =>
        Contrast("#ffffff", hex) >= Contrast("#1a1410", hex) ? "#ffffff" : "#1a1410";

    private static double RelativeLuminance(string hex)
    {
        var h = hex.TrimStart('#');
        double Channel(int offset)
        {
            var c = Convert.ToInt32(h.Substring(offset, 2), 16) / 255.0;
            return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Channel(0) + 0.7152 * Channel(2) + 0.0722 * Channel(4);
    }
}

public sealed record ThemePreset(string Key, string Name, string Description, string Primary, string Dark,
    string Background, string Text, string HeadingFont, string BodyFont, string CornerStyle,
    string AdminAccent, string AdminSidebar, string AdminBackground);
