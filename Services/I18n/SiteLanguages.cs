using System.Globalization;
using Microsoft.AspNetCore.Http;

namespace RestoranYonetim.Services.I18n;

// Müşteri tarafının dilleri. Türkçe öneksiz, diğer diller "/{dil}" önekiyle sunulur.
public static class SiteLanguages
{
    public const string Default = "tr";
    public static readonly IReadOnlyList<string> All = new[] { "tr", "en" };

    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");

    public static CultureInfo Culture(string lang) => lang == "en" ? English : Turkish;

    public static string Current => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "en" ? "en" : Default;

    public static string OgLocale(string lang) => lang == "en" ? "en_US" : "tr_TR";

    // Aynı sayfanın verilen dildeki adresi (dil seçici ve hreflang).
    public static string PathFor(string lang, PathString path, QueryString query)
    {
        var rest = path.HasValue ? path.Value! : "/";
        var url = lang == Default ? rest : rest == "/" ? "/" + lang : "/" + lang + rest;
        return url + query.ToUriComponent();
    }
}
