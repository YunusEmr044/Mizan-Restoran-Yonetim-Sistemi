using RestoranYonetim.Services;

namespace RestoranYonetim.Tests;

public class DecimalInputTests
{
    [Theory]
    [InlineData("750.00", 750.00)]
    [InlineData("750,00", 750.00)]
    [InlineData("12.5", 12.5)]
    [InlineData("12,5", 12.5)]
    [InlineData("1.250,50", 1250.50)]
    [InlineData("1,250.50", 1250.50)]
    [InlineData("12", 12)]
    [InlineData(" 7 ", 7)]
    [InlineData("-40.25", -40.25)]
    public void TryParse_AcceptsDotAndCommaDecimals(string input, double expected)
    {
        Assert.True(DecimalInput.TryParse(input, out var value));
        Assert.Equal((decimal)expected, value);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("1.2.3,4,5")]
    public void TryParse_RejectsInvalid(string? input)
    {
        Assert.False(DecimalInput.TryParse(input, out _));
    }
}
