using RestoranYonetim.Services.Publishing;

namespace RestoranYonetim.Tests;

public class LlmsTxtBuilderTests
{
    private static readonly LlmsTxtInput Input = new(
        "https://m.com/", "Mizan", "Özet", "Taksim", "0212", "Her gün 10:00 - 22:00",
        new LlmsTxtPage[]
        {
            new("Menü", "/menu", "Güncel menü"),
            new("Rezervasyon", "/rezervasyon", null),
            new("SSS", "/sss", null),
            new("İletişim", "/iletisim", null)
        },
        new[] { "Kahvaltı", "Çorbalar" });

    [Fact]
    public void Build_StartsWithTitle()
    {
        Assert.StartsWith("# Mizan\n", LlmsTxtBuilder.Build(Input));
    }

    [Fact]
    public void Build_IncludesSummaryQuote()
    {
        Assert.Contains("\n> Özet\n", LlmsTxtBuilder.Build(Input));
    }

    [Fact]
    public void Build_OmitsSummaryWhenEmpty()
    {
        Assert.DoesNotContain(">", LlmsTxtBuilder.Build(Input with { Summary = " " }));
    }

    [Fact]
    public void Build_IncludesPageLinksWithoutDoubleSlash()
    {
        var text = LlmsTxtBuilder.Build(Input);

        Assert.Contains("- [Menü](https://m.com/menu): Güncel menü\n", text);
        Assert.Contains("- [SSS](https://m.com/sss)\n", text);
        Assert.Contains("(https://m.com/rezervasyon)", text);
        Assert.Contains("(https://m.com/sss)", text);
        Assert.Contains("(https://m.com/iletisim)", text);
        Assert.DoesNotContain("m.com//", text);
    }

    [Fact]
    public void Build_ListsCategories()
    {
        var text = LlmsTxtBuilder.Build(Input);

        Assert.Contains("## Menü kategorileri", text);
        Assert.Contains("- Kahvaltı", text);
        Assert.Contains("- Çorbalar", text);
    }

    [Fact]
    public void Build_IncludesContactLine()
    {
        Assert.Contains("Adres: Taksim | Telefon: 0212 | Çalışma saatleri: Her gün 10:00 - 22:00", LlmsTxtBuilder.Build(Input));
    }
}
