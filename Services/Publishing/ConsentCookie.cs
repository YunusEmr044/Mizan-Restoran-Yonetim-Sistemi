using RestoranYonetim.Models;

namespace RestoranYonetim.Services.Publishing;

public enum ConsentState { Unknown, Granted, Denied }

// Çerez onayı tarayıcıda wwwroot/js/consent.js tarafından yazılır; sunucu yalnızca okur.
public static class ConsentCookie
{
    public const string Name = "mizan_consent";

    public static ConsentState Parse(string? value) => value switch
    {
        "v1:analytics=1" => ConsentState.Granted,
        "v1:analytics=0" => ConsentState.Denied,
        _ => ConsentState.Unknown
    };

    public static bool ShouldShowBanner(AnalyticsProvider provider, ConsentState state) =>
        AnalyticsSettings.RequiresConsent(provider) && state == ConsentState.Unknown;
}
