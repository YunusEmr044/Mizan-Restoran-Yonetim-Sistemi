using RestoranYonetim.Models;
using RestoranYonetim.Services.Publishing;

namespace RestoranYonetim.Tests;

public class ConsentCookieTests
{
    [Theory]
    [InlineData("v1:analytics=1", ConsentState.Granted)]
    [InlineData("v1:analytics=0", ConsentState.Denied)]
    [InlineData(null, ConsentState.Unknown)]
    [InlineData("", ConsentState.Unknown)]
    [InlineData("v2:analytics=1", ConsentState.Unknown)]
    [InlineData("çöp", ConsentState.Unknown)]
    public void Parse_ReadsState(string? value, ConsentState expected)
    {
        Assert.Equal(expected, ConsentCookie.Parse(value));
    }

    [Theory]
    [InlineData(AnalyticsProvider.GoogleAnalytics4, ConsentState.Unknown, true)]
    [InlineData(AnalyticsProvider.GoogleAnalytics4, ConsentState.Granted, false)]
    [InlineData(AnalyticsProvider.GoogleAnalytics4, ConsentState.Denied, false)]
    [InlineData(AnalyticsProvider.Plausible, ConsentState.Unknown, false)]
    [InlineData(AnalyticsProvider.Yok, ConsentState.Unknown, false)]
    public void ShouldShowBanner(AnalyticsProvider provider, ConsentState state, bool expected)
    {
        Assert.Equal(expected, ConsentCookie.ShouldShowBanner(provider, state));
    }
}
