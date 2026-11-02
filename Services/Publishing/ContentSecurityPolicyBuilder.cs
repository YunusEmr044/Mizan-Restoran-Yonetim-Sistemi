using RestoranYonetim.Models;

namespace RestoranYonetim.Services.Publishing;

// CSP script-src/style-src 'unsafe-inline' içeriyor çünkü görünümlerde inline <script>/style="" kullanılıyor; nonce tabanlı CSP'ye geçiş ileri bir aşamaya bırakıldı.
// Seçili analytics servisinin adresleri yalnızca o servis açıkken eklenir.
public sealed record AnalyticsCsp(AnalyticsProvider Provider, string? ScriptUrl);

public static class ContentSecurityPolicyBuilder
{
    // Analytics scriptlerini basan çerez bileşeni bu anahtara yazar; CSP analytics adreslerine yalnızca o sayfada izin verir.
    public const string AnalyticsItemKey = "csp-analytics";

    public static string Build(AnalyticsProvider provider, string? umamiScriptUrl)
    {
        var script = new List<string> { "'self'", "'unsafe-inline'", "https://cdn.jsdelivr.net" };
        var connect = new List<string> { "'self'" };
        var img = new List<string> { "'self'", "data:" };

        switch (provider)
        {
            case AnalyticsProvider.GoogleAnalytics4:
                script.Add("https://www.googletagmanager.com");
                connect.AddRange(new[] { "https://*.google-analytics.com", "https://*.analytics.google.com", "https://www.googletagmanager.com" });
                img.AddRange(new[] { "https://*.google-analytics.com", "https://www.googletagmanager.com" });
                break;
            case AnalyticsProvider.Plausible:
                script.Add("https://plausible.io");
                connect.Add("https://plausible.io");
                break;
            case AnalyticsProvider.Umami when AnalyticsSettings.TryGetHttpsOrigin(umamiScriptUrl, out var origin):
                script.Add(origin);
                connect.Add(origin);
                break;
        }

        return
            "default-src 'self'; " +
            $"script-src {string.Join(' ', script)}; " +
            "style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net; " +
            "font-src 'self' https://cdn.jsdelivr.net; " +
            $"img-src {string.Join(' ', img)}; " +
            $"connect-src {string.Join(' ', connect)}; " +
            "frame-src 'self' https://www.google.com; " +
            "object-src 'none'; " +
            "base-uri 'self'; " +
            "form-action 'self'; " +
            "frame-ancestors 'self';";
    }
}
