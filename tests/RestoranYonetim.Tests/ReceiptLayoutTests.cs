using RestoranYonetim.Services;

namespace RestoranYonetim.Tests;

public class ReceiptLayoutTests
{
    [Theory]
    [InlineData("80mm", "80mm")]
    [InlineData("58mm", "58mm")]
    [InlineData("a4", "a4")]
    [InlineData("A5", "a5")]
    [InlineData("letter", "80mm")]
    [InlineData(null, "80mm")]
    public void SafePaper_FallsBackToThermal80(string? input, string expected)
    {
        Assert.Equal(expected, ReceiptLayout.SafePaper(input));
    }

    [Theory]
    [InlineData("small", "small")]
    [InlineData("LARGE", "large")]
    [InlineData("dev", "normal")]
    [InlineData(null, "normal")]
    public void SafeFontSize_FallsBackToNormal(string? input, string expected)
    {
        Assert.Equal(expected, ReceiptLayout.SafeFontSize(input));
    }

    [Theory]
    [InlineData("80mm", 80, true)]
    [InlineData("58mm", 58, true)]
    [InlineData("a5", 148, false)]
    [InlineData("a4", 210, false)]
    public void Paper_WidthAndRoll(string paper, int widthMm, bool isRoll)
    {
        var p = ReceiptLayout.Paper(paper);
        Assert.Equal(widthMm, p.WidthMm);
        Assert.Equal(isRoll, p.IsRoll);
    }

    [Fact]
    public void SheetPapersUseValidCssPageSize()
    {
        Assert.Equal("A4 portrait", ReceiptLayout.Paper("a4").CssPageSize);
        Assert.Equal("A5 portrait", ReceiptLayout.Paper("a5").CssPageSize);
        Assert.Null(ReceiptLayout.Paper("80mm").CssPageSize);
    }

    [Fact]
    public void FontGrowsWithSetting_AndNarrowPaperIsSmaller()
    {
        Assert.True(ReceiptLayout.FontPx("80mm", "small") < ReceiptLayout.FontPx("80mm", "normal"));
        Assert.True(ReceiptLayout.FontPx("80mm", "normal") < ReceiptLayout.FontPx("80mm", "large"));
        Assert.True(ReceiptLayout.FontPx("58mm", "normal") < ReceiptLayout.FontPx("80mm", "normal"));
    }

    [Theory]
    [InlineData(1640, "1.640,00")]
    [InlineData(0.5, "0,50")]
    [InlineData(1234567.891, "1.234.567,89")]
    [InlineData(-200, "-200,00")]
    public void Money_UsesTurkishFormat(double value, string expected)
    {
        Assert.Equal(expected, ReceiptLayout.Money((decimal)value));
    }

    [Fact]
    public void PaperOptionsCoverEverySafeValue()
    {
        Assert.Equal(new[] { "80mm", "58mm", "a5", "a4" }, ReceiptLayout.PaperOptions.Select(o => o.Key));
        Assert.All(ReceiptLayout.PaperOptions, o => Assert.Equal(o.Key, ReceiptLayout.SafePaper(o.Key)));
    }
}
