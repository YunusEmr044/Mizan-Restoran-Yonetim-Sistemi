using RestoranYonetim.Services.I18n;

namespace RestoranYonetim.Tests;

public class TranslationCatalogTests
{
    [Fact]
    public void Every_Translatable_Type_Is_In_A_Tab()
    {
        var inTabs = TranslationCatalog.Tabs.SelectMany(t => t.EntityTypes).ToList();

        Assert.Equal(TranslationOverlay.Fields.Keys.OrderBy(k => k), inTabs.OrderBy(k => k));
        Assert.Equal(inTabs.Count, inTabs.Distinct().Count());
    }

    [Fact]
    public void Rejects_Unknown_Field()
    {
        Assert.True(TranslationCatalog.IsAllowed("menu", "MenuItem", "Name"));
        Assert.False(TranslationCatalog.IsAllowed("menu", "MenuItem", "ImageUrl"));
        Assert.False(TranslationCatalog.IsAllowed("menu", "Faq", "Question"));
        Assert.False(TranslationCatalog.IsAllowed("nope", "MenuItem", "Name"));
    }

    [Theory]
    [InlineData("Body", "kısa", true)]
    [InlineData("Name", "Adana Kebap", false)]
    [InlineData("Title", "Çok uzun bir başlık metni ki seksen karakteri aşsın diye uzatılmış bir örnek metin burada", true)]
    public void Long_Fields_Use_Textarea(string field, string turkish, bool expected)
    {
        Assert.Equal(expected, TranslationCatalog.IsLong(field, turkish));
    }
}
