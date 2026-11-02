using System.Text.RegularExpressions;

namespace RestoranYonetim.Services.I18n;

// Varsayılan "Masa N" adları İngilizce sayfada "Table N" görünür; restoranın kendi verdiği adlara dokunulmaz.
public static partial class TableNames
{
    [GeneratedRegex(@"^Masa (\d+)$")]
    private static partial Regex DefaultName();

    public static string Display(string name, string language)
    {
        var match = DefaultName().Match(name.Trim());
        return language == "en" && match.Success ? $"Table {match.Groups[1].Value}" : name;
    }

    public static string Display(string name) => Display(name, SiteLanguages.Current);
}
