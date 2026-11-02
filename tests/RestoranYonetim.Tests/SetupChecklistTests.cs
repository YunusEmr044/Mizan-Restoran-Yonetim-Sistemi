using RestoranYonetim.Services;

namespace RestoranYonetim.Tests;

public class SetupChecklistTests
{
    private static readonly SetupSnapshot Empty = new(
        HeroTitle: "Restoranımıza Hoş Geldiniz", LogoUrl: null, FaviconUrl: null, Phone: "0000 000 00 00", Address: null,
        PublicBaseUrlSet: false, Tables: 0, MenuItems: 0);

    [Fact]
    public void Fresh_Install_Has_All_Steps_Open()
    {
        var steps = SetupChecklist.Build(Empty);
        Assert.Equal(6, steps.Count);
        Assert.All(steps, s => Assert.False(s.Done));
        Assert.False(SetupChecklist.IsComplete(steps));
    }

    [Fact]
    public void Steps_Complete_From_Real_Values()
    {
        var steps = SetupChecklist.Build(new SetupSnapshot("Örnek Ocakbaşı", "/uploads/logo.webp", "/uploads/icon.png", "0212 555 00 00",
            "Kadıköy, İstanbul", PublicBaseUrlSet: true, Tables: 12, MenuItems: 40));
        Assert.All(steps, s => Assert.True(s.Done, s.Key));
        Assert.True(SetupChecklist.IsComplete(steps));
    }

    [Fact]
    public void Logo_Step_Needs_Both_Logo_And_Icon()
    {
        var steps = SetupChecklist.Build(Empty with { LogoUrl = "/uploads/logo.webp" });
        Assert.False(steps.Single(s => s.Key == "logo").Done);
    }

    [Theory]
    [InlineData("Restoranımıza Hoş Geldiniz", false)]
    [InlineData("  ", false)]
    [InlineData(null, false)]
    [InlineData("Örnek Ocakbaşı", true)]
    public void Placeholder_Texts_Do_Not_Count_As_Filled(string? value, bool expected)
    {
        Assert.Equal(expected, SetupChecklist.Filled(value));
    }

    [Theory]
    [InlineData(new string[0], 3, new[] { "Masa 1", "Masa 2", "Masa 3" })]
    [InlineData(new[] { "Masa 1", "Masa 2", "Bahçe 4" }, 2, new[] { "Masa 3", "Masa 4" })]
    [InlineData(new[] { "Masa 9" }, 1, new[] { "Masa 10" })]
    public void Table_Names_Continue_Numbering(string[] existing, int count, string[] expected)
    {
        Assert.Equal(expected, SetupChecklist.NewTableNames(existing, count));
    }
}
