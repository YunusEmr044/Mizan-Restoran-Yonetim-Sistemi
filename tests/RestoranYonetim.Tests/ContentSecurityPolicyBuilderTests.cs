using RestoranYonetim.Models;
using RestoranYonetim.Services.Publishing;

namespace RestoranYonetim.Tests;

public class ContentSecurityPolicyBuilderTests
{
    private const string LegacyPolicy =
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net; " +
        "style-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net; " +
        "font-src 'self' https://cdn.jsdelivr.net; " +
        "img-src 'self' data:; " +
        "connect-src 'self'; " +
        "frame-src 'self' https://www.google.com; " +
        "object-src 'none'; " +
        "base-uri 'self'; " +
        "form-action 'self'; " +
        "frame-ancestors 'self';";

    private static string Directive(string policy, string name) =>
        policy.Split(';', StringSplitOptions.TrimEntries).Single(d => d.StartsWith(name + " "));

    [Fact]
    public void Build_None_EqualsLegacyPolicy()
    {
        Assert.Equal(LegacyPolicy, ContentSecurityPolicyBuilder.Build(AnalyticsProvider.Yok, null));
    }

    [Fact]
    public void Build_Ga4_AddsGoogleSources()
    {
        var policy = ContentSecurityPolicyBuilder.Build(AnalyticsProvider.GoogleAnalytics4, null);

        Assert.Contains("https://www.googletagmanager.com", Directive(policy, "script-src"));
        Assert.Contains("https://*.google-analytics.com", Directive(policy, "connect-src"));
        Assert.Contains("https://*.analytics.google.com", Directive(policy, "connect-src"));
        Assert.Contains("https://*.google-analytics.com", Directive(policy, "img-src"));
    }

    [Fact]
    public void Build_Plausible_AddsPlausible()
    {
        var policy = ContentSecurityPolicyBuilder.Build(AnalyticsProvider.Plausible, null);

        Assert.Contains("https://plausible.io", Directive(policy, "script-src"));
        Assert.Contains("https://plausible.io", Directive(policy, "connect-src"));
    }

    [Fact]
    public void Build_Umami_AddsScriptOrigin()
    {
        var policy = ContentSecurityPolicyBuilder.Build(AnalyticsProvider.Umami, "https://u.example.com/script.js");

        Assert.EndsWith("https://u.example.com", Directive(policy, "script-src"));
        Assert.EndsWith("https://u.example.com", Directive(policy, "connect-src"));
        Assert.DoesNotContain("script.js", policy);
    }

    [Fact]
    public void Build_Umami_InvalidUrl_IgnoresSource()
    {
        Assert.Equal(LegacyPolicy, ContentSecurityPolicyBuilder.Build(AnalyticsProvider.Umami, "http://u.example.com/script.js"));
    }
}
