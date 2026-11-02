using System.Globalization;
using Microsoft.AspNetCore.Http;

namespace RestoranYonetim.Services.I18n;

// "/en/..." isteğinde öneki PathBase'e taşır ve kültürü İngilizce yapar; böylece üretilen bütün bağlantılar "/en" ile başlar.
// Personel tarafı Türkçe kaldığı için önekle gelen panel/personel yolları öneksiz adrese yönlendirilir.
public class LanguagePrefixMiddleware
{
    private static readonly string[] StaffPrefixes = { "/admin", "/staff", "/kasa", "/mutfak", "/bar", "/yazici", "/account", "/hubs" };

    private const string LanguageKey = "site-language";

    private readonly RequestDelegate _next;

    public LanguagePrefixMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public static (string Lang, PathString Remainder, string? RedirectTo) Resolve(PathString path, QueryString query)
    {
        foreach (var lang in SiteLanguages.All.Where(l => l != SiteLanguages.Default))
        {
            if (!path.StartsWithSegments("/" + lang, StringComparison.OrdinalIgnoreCase, out var rest))
            {
                continue;
            }
            var restValue = rest.HasValue && rest.Value != "/" ? rest.Value! : "/";
            var isStaff = StaffPrefixes.Any(p => restValue.Equals(p, StringComparison.OrdinalIgnoreCase)
                || restValue.StartsWith(p + "/", StringComparison.OrdinalIgnoreCase));
            return isStaff ? (SiteLanguages.Default, restValue, restValue + query.ToUriComponent()) : (lang, restValue, null);
        }
        return (SiteLanguages.Default, path, null);
    }

    public Task InvokeAsync(HttpContext context)
    {
        var (lang, rest, redirectTo) = Resolve(context.Request.Path, context.Request.QueryString);
        // Hata sayfası isteği öneksiz yolla yeniden çalıştırır; ilk geçişte belirlenen dil korunur.
        if (lang == SiteLanguages.Default && redirectTo == null && context.Items[LanguageKey] is string previous && previous != SiteLanguages.Default)
        {
            lang = previous;
            rest = context.Request.Path;
        }
        context.Items[LanguageKey] = lang;
        if (redirectTo != null)
        {
            context.Response.Redirect(context.Request.PathBase + redirectTo);
            return Task.CompletedTask;
        }
        if (lang != SiteLanguages.Default)
        {
            context.Request.PathBase = context.Request.PathBase.Add("/" + lang);
            context.Request.Path = rest;
        }

        var culture = SiteLanguages.Culture(lang);
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        return _next(context);
    }
}
