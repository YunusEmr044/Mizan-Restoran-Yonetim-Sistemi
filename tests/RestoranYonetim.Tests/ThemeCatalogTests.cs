using RestoranYonetim.Models;

namespace RestoranYonetim.Tests;

public class ThemeCatalogTests
{
    [Fact]
    public void Contrast_KnownPairs()
    {
        Assert.Equal(21, ThemeCatalog.Contrast("#000000", "#ffffff"), 2);
        Assert.Equal(1, ThemeCatalog.Contrast("#cd9f56", "#cd9f56"), 2);
        Assert.Equal(ThemeCatalog.Contrast("#cd9f56", "#100d0a"), ThemeCatalog.Contrast("#100d0a", "#cd9f56"), 6);
    }

    [Theory]
    [InlineData("#cd9f56", "#1a1410")]
    [InlineData("#b5485d", "#ffffff")]
    [InlineData("#ffffff", "#1a1410")]
    public void ReadableTextOn_PicksHigherContrast(string background, string expected)
    {
        Assert.Equal(expected, ThemeCatalog.ReadableTextOn(background));
    }

    public static TheoryData<string> PresetKeys => new() { "altin", "bordo", "zeytin", "gece" };

    [Theory]
    [MemberData(nameof(PresetKeys))]
    public void Presets_PassContrastAndSafety(string key)
    {
        var p = ThemeCatalog.Presets.Single(x => x.Key == key);

        foreach (var color in new[] { p.Primary, p.Dark, p.Background, p.Text, p.AdminAccent, p.AdminSidebar, p.AdminBackground })
        {
            Assert.Equal(color, ThemeCatalog.SafeColor(color, "#000000"));
        }
        Assert.Equal(p.HeadingFont, ThemeCatalog.SafeHeadingFont(p.HeadingFont));
        Assert.Equal(p.BodyFont, ThemeCatalog.SafeBodyFont(p.BodyFont));
        Assert.Equal(p.CornerStyle, ThemeCatalog.SafeCornerStyle(p.CornerStyle));

        Assert.True(ThemeCatalog.Contrast(p.Text, p.Background) >= 4.5, "metin/zemin");
        Assert.True(ThemeCatalog.Contrast(ThemeCatalog.ReadableTextOn(p.Primary), p.Primary) >= 4.5, "düğme metni");
        Assert.True(ThemeCatalog.Contrast("#ffffff", p.Dark) >= 4.5, "koyu zeminde beyaz");
        Assert.True(ThemeCatalog.Contrast(p.Primary, p.Dark) >= 3.0, "koyu zeminde vurgu");
        Assert.True(ThemeCatalog.Contrast(ThemeCatalog.PrimaryLightFor(p.Primary), p.Dark) >= 4.5, "koyu zeminde açık vurgu metni");
        Assert.True(ThemeCatalog.Contrast(ThemeCatalog.ReadableTextOn(p.AdminAccent), p.AdminAccent) >= 4.5, "panel düğme metni");
    }

    [Fact]
    public void Presets_KeysAndDefault()
    {
        Assert.Equal(new[] { "altin", "bordo", "zeytin", "gece" }, ThemeCatalog.Presets.Select(p => p.Key));

        var altin = ThemeCatalog.Presets[0];
        Assert.Equal(ThemeCatalog.DefaultPrimary, altin.Primary);
        Assert.Equal(ThemeCatalog.DefaultDark, altin.Dark);
        Assert.Equal(ThemeCatalog.DefaultBackground, altin.Background);
        Assert.Equal(ThemeCatalog.DefaultText, altin.Text);
        Assert.Equal(ThemeCatalog.DefaultHeadingFont, altin.HeadingFont);
        Assert.Equal(ThemeCatalog.DefaultBodyFont, altin.BodyFont);
        Assert.Equal(ThemeCatalog.DefaultCornerStyle, altin.CornerStyle);
        Assert.Equal(ThemeCatalog.DefaultAdminAccent, altin.AdminAccent);
        Assert.Equal(ThemeCatalog.DefaultAdminSidebar, altin.AdminSidebar);
        Assert.Equal(ThemeCatalog.DefaultAdminBackground, altin.AdminBackground);
    }

    [Fact]
    public void FontStylesheets_UsesFontsourceWeights()
    {
        const string root = "https://cdn.jsdelivr.net/npm/@fontsource/";
        var urls = ThemeCatalog.FontStylesheets("Playfair Display", "Poppins");

        Assert.Equal(new[]
        {
            root + "playfair-display@5.3.0/600.css", root + "playfair-display@5.3.0/700.css", root + "playfair-display@5.3.0/800.css",
            root + "poppins@5.3.0/300.css", root + "poppins@5.3.0/400.css", root + "poppins@5.3.0/500.css",
            root + "poppins@5.3.0/600.css", root + "poppins@5.3.0/700.css"
        }, urls);
    }

    [Fact]
    public void StylesheetsFor_SingleFontFromEitherList()
    {
        const string root = "https://cdn.jsdelivr.net/npm/@fontsource/";

        Assert.Equal(new[] { root + "lora@5.3.0/400.css", root + "lora@5.3.0/500.css", root + "lora@5.3.0/600.css", root + "lora@5.3.0/700.css" },
            ThemeCatalog.StylesheetsFor("Lora"));
        Assert.Equal(new[] { root + "merriweather@5.3.0/700.css", root + "merriweather@5.3.0/900.css" },
            ThemeCatalog.StylesheetsFor("Merriweather"));
        Assert.Empty(ThemeCatalog.StylesheetsFor("Comic Sans"));
    }

    [Fact]
    public void FontStylesheets_DedupesAndFallsBack()
    {
        var same = ThemeCatalog.FontStylesheets("Lora", "Lora");
        Assert.Equal(same.Distinct().Count(), same.Count);

        Assert.Equal(ThemeCatalog.FontStylesheets(ThemeCatalog.DefaultHeadingFont, ThemeCatalog.DefaultBodyFont),
            ThemeCatalog.FontStylesheets("Comic Sans", "Yok"));
    }
}
