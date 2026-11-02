using RestoranYonetim.Services.Publishing;

namespace RestoranYonetim.Tests;

public class LegalTemplateTests
{
    private static readonly LegalTemplateValues Full = new("Mizan", "Taksim", "a@b.c", "0212", "https://m.com");

    [Fact]
    public void Fill_ReplacesAllTokens()
    {
        var result = LegalTemplate.Fill("{RestoranAdi} {Adres} {Eposta} {Telefon} {SiteAdresi}", Full);

        Assert.Equal("Mizan Taksim a@b.c 0212 https://m.com", result);
    }

    [Fact]
    public void Fill_EmptyValueBecomesDash()
    {
        var result = LegalTemplate.Fill("Adres: {Adres}", Full with { Address = null });

        Assert.Equal("Adres: —", result);
    }

    [Fact]
    public void Fill_EscapesMarkdownInValues()
    {
        var result = LegalTemplate.Fill("{RestoranAdi}", Full with { RestaurantName = "*Mizan* [x](y)" });

        Assert.Equal("\\*Mizan\\* \\[x\\](y)", result);
    }

    [Fact]
    public void Fill_UnknownTokenUntouched()
    {
        var result = LegalTemplate.Fill("{Bilinmeyen}", Full);

        Assert.Equal("{Bilinmeyen}", result);
    }
}
