using RestoranYonetim.Models;
using RestoranYonetim.Services.I18n;

namespace RestoranYonetim.Tests;

public class TranslationTests
{
    [Fact]
    public void Translatable_Fields_Match_Spec()
    {
        var expected = new Dictionary<string, string[]>
        {
            ["MenuItem"] = new[] { "Name", "Description" },
            ["Category"] = new[] { "Name" },
            ["MenuItemOption"] = new[] { "Name" },
            ["Faq"] = new[] { "Question", "Answer", "Category" },
            ["Campaign"] = new[] { "Name", "Description", "ButtonText" },
            ["GalleryImage"] = new[] { "Caption" },
            ["ChefSpecial"] = new[] { "TitleOverride", "DescriptionOverride" },
            ["LegalPage"] = new[] { "Title", "Body" },
            ["PageSeo"] = new[] { "Title", "MetaDescription", "OgTitle", "OgDescription" },
            ["HeroSlide"] = new[] { "Title", "Subtitle", "Description", "PrimaryButtonText", "SecondaryButtonText" },
            ["HomeStat"] = new[] { "Value", "Label" },
            ["HomeFeature"] = new[] { "Title", "Description" },
        };

        foreach (var (type, fields) in expected)
        {
            Assert.Equal(fields.OrderBy(f => f), TranslationOverlay.Fields[type].OrderBy(f => f));
        }
        Assert.Equal(expected.Keys.Append("SiteContent").OrderBy(k => k), TranslationOverlay.Fields.Keys.OrderBy(k => k));
    }

    [Theory]
    [InlineData("HeroTitle", true)]
    [InlineData("QrStep2Text", true)]
    [InlineData("WorkingHours", true)]
    [InlineData("WhatsAppDefaultMessage", true)]
    [InlineData("HeroImageUrl", false)]
    [InlineData("ThemePrimaryColor", false)]
    [InlineData("ContactPhone", false)]
    [InlineData("ReceiptFooterText", false)]
    [InlineData("PublicBaseUrl", false)]
    public void SiteContent_Text_Fields_Only(string field, bool translatable)
    {
        Assert.Equal(translatable, TranslationOverlay.Fields["SiteContent"].Contains(field));
    }

    [Fact]
    public void Overlay_Applies_NonEmpty_Only()
    {
        var item = new MenuItem { Id = 3, Name = "Adana Kebap", Description = "Acılı" };
        TranslationOverlay.Apply(item, new Dictionary<string, string> { ["Name"] = "Adana Kebab", ["Description"] = " " });

        Assert.Equal(("Adana Kebab", "Acılı"), (item.Name, item.Description));
    }

    [Fact]
    public void Overlay_Skips_Unknown_Field()
    {
        var item = new MenuItem { Id = 3, Name = "Adana", ImageUrl = "/img/a.jpg" };
        TranslationOverlay.Apply(item, new Dictionary<string, string> { ["ImageUrl"] = "/evil.jpg", ["Price"] = "1" });

        Assert.Equal("/img/a.jpg", item.ImageUrl);
    }

    [Fact]
    public void Cache_Keys_Are_Per_Language()
    {
        Assert.NotEqual(CacheKeys.For("site-content", "en"), CacheKeys.For("site-content", "tr"));
        Assert.Equal(SiteLanguages.All.Count, CacheKeys.AllLanguages("site-content").Distinct().Count());
        Assert.Contains(CacheKeys.For("site-content", "en"), CacheKeys.AllLanguages("site-content"));
    }
}
