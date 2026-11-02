using Microsoft.AspNetCore.Http;
using RestoranYonetim.Services.I18n;

namespace RestoranYonetim.Tests;

public class LanguagePrefixTests
{
    [Fact]
    public void En_Prefix_Strips_And_Sets_En()
    {
        var r = LanguagePrefixMiddleware.Resolve("/en/menu", QueryString.Empty);

        Assert.Equal(("en", "/menu", (string?)null), (r.Lang, r.Remainder.Value, r.RedirectTo));
    }

    [Fact]
    public void Bare_En_Is_Root()
    {
        Assert.Equal("/", LanguagePrefixMiddleware.Resolve("/en", QueryString.Empty).Remainder.Value);
        Assert.Equal("/", LanguagePrefixMiddleware.Resolve("/EN/", QueryString.Empty).Remainder.Value);
    }

    [Fact]
    public void No_Prefix_Is_Tr()
    {
        var r = LanguagePrefixMiddleware.Resolve("/menu", QueryString.Empty);

        Assert.Equal(("tr", "/menu"), (r.Lang, r.Remainder.Value));
    }

    [Fact]
    public void Eng_Is_Not_Prefix()
    {
        var r = LanguagePrefixMiddleware.Resolve("/eng/x", QueryString.Empty);

        Assert.Equal(("tr", "/eng/x"), (r.Lang, r.Remainder.Value));
    }

    [Theory]
    [InlineData("/en/Admin/Dashboard", "?x=1", "/Admin/Dashboard?x=1")]
    [InlineData("/en/admin", "", "/admin")]
    [InlineData("/en/hubs/ops", "", "/hubs/ops")]
    [InlineData("/en/Kasa", "", "/Kasa")]
    [InlineData("/en/staff/Paketler", "", "/staff/Paketler")]
    [InlineData("/en/Account/Login", "", "/Account/Login")]
    public void Staff_Paths_Redirect(string path, string query, string expected)
    {
        Assert.Equal(expected, LanguagePrefixMiddleware.Resolve(path, new QueryString(query == "" ? null : query)).RedirectTo);
    }

    [Fact]
    public void Public_Paths_Under_En_Do_Not_Redirect()
    {
        Assert.Null(LanguagePrefixMiddleware.Resolve("/en/menu/abc", QueryString.Empty).RedirectTo);
        Assert.Null(LanguagePrefixMiddleware.Resolve("/en/kasaba", QueryString.Empty).RedirectTo);
    }

    [Fact]
    public void PathFor_Builds_Other_Language()
    {
        Assert.Equal("/en/menu?a=1", SiteLanguages.PathFor("en", "/menu", new QueryString("?a=1")));
        Assert.Equal("/", SiteLanguages.PathFor("tr", "/", QueryString.Empty));
        Assert.Equal("/en", SiteLanguages.PathFor("en", "/", QueryString.Empty));
        Assert.Equal("/menu", SiteLanguages.PathFor("tr", "/menu", QueryString.Empty));
    }

    [Fact]
    public void Cultures()
    {
        Assert.Equal("tr-TR", SiteLanguages.Culture("tr").Name);
        Assert.Equal("en-US", SiteLanguages.Culture("en").Name);
        Assert.Equal("tr-TR", SiteLanguages.Culture("xx").Name);
    }
}
