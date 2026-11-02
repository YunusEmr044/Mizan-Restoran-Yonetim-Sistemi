using System.Text.RegularExpressions;
using RestoranYonetim.Models;

namespace RestoranYonetim.Services.Publishing;

public static partial class AnalyticsSettings
{
    // Geçerliyse null, değilse kullanıcıya gösterilecek Türkçe hata mesajı.
    public static string? Validate(AnalyticsProvider provider, string? id, string? scriptUrl)
    {
        id = id?.Trim();
        switch (provider)
        {
            case AnalyticsProvider.Yok:
                return null;
            case AnalyticsProvider.GoogleAnalytics4:
                return id != null && Ga4IdRegex().IsMatch(id)
                    ? null
                    : "Google Analytics 4 ölçüm kimliği \"G-\" ile başlamalı (ör. G-ABC123XYZ9).";
            case AnalyticsProvider.Plausible:
                return id != null && DomainRegex().IsMatch(id)
                    ? null
                    : "Plausible için sitenizin alan adını \"https://\" olmadan girin (ör. restoraniniz.com).";
            case AnalyticsProvider.Umami:
                if (!Guid.TryParse(id, out _))
                {
                    return "Umami Website ID bir UUID olmalı (ör. 3f2b9c1e-7a4d-4e8b-9c2f-1a2b3c4d5e6f).";
                }
                return TryGetHttpsOrigin(scriptUrl, out _)
                    ? null
                    : "Umami script adresi \"https://\" ile başlayan tam bir adres olmalı (ör. https://analytics.restoraniniz.com/script.js).";
            default:
                return "Geçersiz analytics servisi.";
        }
    }

    public static bool RequiresConsent(AnalyticsProvider provider) => provider == AnalyticsProvider.GoogleAnalytics4;

    public static bool TryGetHttpsOrigin(string? url, out string origin)
    {
        origin = string.Empty;
        if (!Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            return false;
        }
        origin = uri.GetLeftPart(UriPartial.Authority);
        return true;
    }

    [GeneratedRegex("^G-[A-Z0-9]{6,12}$")]
    private static partial Regex Ga4IdRegex();

    [GeneratedRegex(@"^(?=.{1,253}$)([a-z0-9](-?[a-z0-9])*\.)+[a-z]{2,}$", RegexOptions.IgnoreCase)]
    private static partial Regex DomainRegex();
}
