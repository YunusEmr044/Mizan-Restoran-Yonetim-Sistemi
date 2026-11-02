using RestoranYonetim.Models;
using RestoranYonetim.Services.Publishing;

namespace RestoranYonetim.Tests;

public class AnalyticsSettingsTests
{
    private const string UmamiId = "3f2b9c1e-7a4d-4e8b-9c2f-1a2b3c4d5e6f";

    [Theory]
    [InlineData(null, null)]
    [InlineData("çöp", "http://x")]
    public void Validate_None_AlwaysValid(string? id, string? url)
    {
        Assert.Null(AnalyticsSettings.Validate(AnalyticsProvider.Yok, id, url));
    }

    [Theory]
    [InlineData("G-ABC1234", true)]
    [InlineData("UA-123", false)]
    [InlineData("G-ABC\"><script>", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Validate_Ga4(string? id, bool valid)
    {
        Assert.Equal(valid, AnalyticsSettings.Validate(AnalyticsProvider.GoogleAnalytics4, id, null) == null);
    }

    [Theory]
    [InlineData("mizan.com", true)]
    [InlineData("alt.mizan.com.tr", true)]
    [InlineData("http://x", false)]
    [InlineData("mizan", false)]
    public void Validate_Plausible(string id, bool valid)
    {
        Assert.Equal(valid, AnalyticsSettings.Validate(AnalyticsProvider.Plausible, id, null) == null);
    }

    [Theory]
    [InlineData(UmamiId, "https://u.example.com/script.js", true)]
    [InlineData(UmamiId, "http://u.example.com/script.js", false)]
    [InlineData("not-a-uuid", "https://u.example.com/script.js", false)]
    [InlineData(UmamiId, null, false)]
    public void Validate_Umami(string id, string? url, bool valid)
    {
        Assert.Equal(valid, AnalyticsSettings.Validate(AnalyticsProvider.Umami, id, url) == null);
    }

    [Theory]
    [InlineData(AnalyticsProvider.GoogleAnalytics4, true)]
    [InlineData(AnalyticsProvider.Plausible, false)]
    [InlineData(AnalyticsProvider.Umami, false)]
    [InlineData(AnalyticsProvider.Yok, false)]
    public void RequiresConsent_OnlyGa4(AnalyticsProvider provider, bool expected)
    {
        Assert.Equal(expected, AnalyticsSettings.RequiresConsent(provider));
    }
}
