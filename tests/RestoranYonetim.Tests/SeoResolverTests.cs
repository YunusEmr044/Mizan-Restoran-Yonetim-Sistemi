using RestoranYonetim.Services.Publishing;

namespace RestoranYonetim.Tests;

public class SeoResolverTests
{
    private static readonly SeoInputs Home = new(
        IsHome: true, PageTitle: null, SiteSeoTitle: null, CatalogTitle: "Ana Sayfa",
        PageDescription: null, SiteSeoDescription: null, CatalogDescription: null, DefaultDescription: null,
        HeroTitle: "Mizan", HeroSubtitle: "Taze malzemeler");

    [Fact]
    public void Home_WithoutSeoTitle_UsesHeroTitleAndSubtitle()
    {
        Assert.Equal("Mizan — Taze malzemeler", SeoResolver.Resolve(Home).Title);
    }

    [Fact]
    public void Home_WithoutSubtitle_UsesHeroTitle()
    {
        Assert.Equal("Mizan", SeoResolver.Resolve(Home with { HeroSubtitle = null }).Title);
    }

    [Fact]
    public void Home_SiteSeoTitleWins()
    {
        Assert.Equal("Özel", SeoResolver.Resolve(Home with { SiteSeoTitle = "Özel" }).Title);
    }

    [Fact]
    public void SubPage_AppendsRestaurantName()
    {
        var page = Home with { IsHome = false, CatalogTitle = "Menü" };

        Assert.Equal("Menü | Mizan", SeoResolver.Resolve(page).Title);
    }

    [Fact]
    public void Description_FallsBackToHeroSubtitle()
    {
        Assert.Equal("Taze malzemeler", SeoResolver.Resolve(Home).Description);
    }

    [Fact]
    public void Description_PageDescriptionWins()
    {
        Assert.Equal("Sayfa", SeoResolver.Resolve(Home with { PageDescription = "Sayfa", SiteSeoDescription = "Site" }).Description);
    }
}
