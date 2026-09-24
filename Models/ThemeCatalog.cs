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
    public static readonly Dictionary<string, string> HeadingFonts = new()
    {
        ["Playfair Display"] = "Playfair+Display:wght@600;700;800",
        ["Cormorant Garamond"] = "Cormorant+Garamond:wght@600;700",
        ["Montserrat"] = "Montserrat:wght@600;700;800",
        ["Merriweather"] = "Merriweather:wght@700;900"
    };

    public static readonly Dictionary<string, string> BodyFonts = new()
    {
        ["Poppins"] = "Poppins:wght@300;400;500;600;700",
        ["Inter"] = "Inter:wght@300;400;500;600;700",
        ["Nunito Sans"] = "Nunito+Sans:wght@300;400;600;700",
        ["Lora"] = "Lora:wght@400;500;600;700"
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
        var primaryLight = Lighten(primary, 0.38);
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
        viewBag.ThemeGoogleFontsQuery = BuildGoogleFontsQuery(headingFont, bodyFont);
    }

    public static string BuildGoogleFontsQuery(string headingFont, string bodyFont)
    {
        var families = new List<string>();
        if (HeadingFonts.TryGetValue(headingFont, out var headingFamily))
        {
            families.Add(headingFamily);
        }

        if (BodyFonts.TryGetValue(bodyFont, out var bodyFamily) &&
            !string.Equals(headingFont, bodyFont, StringComparison.Ordinal))
        {
            families.Add(bodyFamily);
        }

        return string.Join("&", families.Select(f => "family=" + f));
    }
}
