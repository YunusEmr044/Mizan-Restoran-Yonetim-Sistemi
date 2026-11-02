using System.Globalization;

namespace RestoranYonetim.Services;

public sealed record ReceiptPaper(string Key, string Label, int WidthMm, bool IsRoll, string? CssPageSize);

// Adisyon çıktısının kağıt ve yazı ölçüleri. Rulo kağıtta sayfa yüksekliği yazdırma anında içeriğe göre hesaplanır.
public static class ReceiptLayout
{
    public const string DefaultPaper = "80mm";
    public const string DefaultFontSize = "normal";

    public static readonly IReadOnlyList<ReceiptPaper> PaperOptions =
    [
        new("80mm", "80 mm termal rulo", 80, true, null),
        new("58mm", "58 mm termal rulo", 58, true, null),
        new("a5", "A5 kağıt", 148, false, "A5 portrait"),
        new("a4", "A4 kağıt", 210, false, "A4 portrait")
    ];

    public static readonly IReadOnlyList<(string Key, string Label)> FontSizeOptions =
        [("small", "Küçük"), ("normal", "Normal"), ("large", "Büyük")];

    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    public static string SafePaper(string? value)
    {
        var key = value?.Trim().ToLowerInvariant();
        return PaperOptions.Any(p => p.Key == key) ? key! : DefaultPaper;
    }

    public static string SafeFontSize(string? value)
    {
        var key = value?.Trim().ToLowerInvariant();
        return FontSizeOptions.Any(f => f.Key == key) ? key! : DefaultFontSize;
    }

    public static ReceiptPaper Paper(string? value) => PaperOptions.Single(p => p.Key == SafePaper(value));

    public static int FontPx(string? paper, string? fontSize)
    {
        var basePx = SafePaper(paper) switch { "58mm" => 10, "80mm" => 12, _ => 13 };
        return basePx + SafeFontSize(fontSize) switch { "small" => -2, "large" => 2, _ => 0 };
    }

    public static string Money(decimal value) => value.ToString("N2", Turkish);
}
