using RestoranYonetim.Models;
using RestoranYonetim.Services;

namespace RestoranYonetim.Tests;

public class CustomerLevelTests
{
    [Theory]
    [InlineData(0, CustomerLevel.Standart)]
    [InlineData(999.99, CustomerLevel.Standart)]
    [InlineData(1000, CustomerLevel.Silver)]
    [InlineData(4999.99, CustomerLevel.Silver)]
    [InlineData(5000, CustomerLevel.Gold)]
    [InlineData(15000, CustomerLevel.VIP)]
    public void LevelFor_UsesSpendThresholds(double spent, CustomerLevel expected)
    {
        Assert.Equal(expected, CustomerService.LevelFor((decimal)spent));
    }
}
