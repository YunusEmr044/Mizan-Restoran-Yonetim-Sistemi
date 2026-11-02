using RestoranYonetim.Services;

namespace RestoranYonetim.Tests;

public class PublicSeoOgImageTests
{
    [Fact]
    public void SkipsWebpAndTakesFirstUsable()
    {
        Assert.Equal("/b.jpg", PublicSeo.PickOgImage(null, "/a.webp", "/b.jpg"));
        Assert.Equal("/x.png", PublicSeo.PickOgImage("/x.png", "/b.jpg"));
    }

    [Fact]
    public void WebpCheckIgnoresCaseQueryAndFragment()
    {
        Assert.Equal(PublicSeo.DefaultOgImage, PublicSeo.PickOgImage(null, "", "/a.WEBP?v=2"));
        Assert.Equal("/l.svg", PublicSeo.PickOgImage("https://cdn.example/a.webp#x", "/l.svg"));
    }

    [Fact]
    public void FallsBackToDefault()
    {
        Assert.Equal(PublicSeo.DefaultOgImage, PublicSeo.PickOgImage());
        Assert.Equal("/img/og-default.png", PublicSeo.DefaultOgImage);
    }
}
