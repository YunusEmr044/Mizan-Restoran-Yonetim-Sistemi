using RestoranYonetim.Services.I18n;

namespace RestoranYonetim.Tests;

public class TableNameTests
{
    [Theory]
    [InlineData("Masa 5", "en", "Table 5")]
    [InlineData("Masa 12", "en", "Table 12")]
    [InlineData("Masa 5", "tr", "Masa 5")]
    [InlineData("Bahçe 3", "en", "Bahçe 3")]
    [InlineData("Masa", "en", "Masa")]
    public void Default_Table_Names_Follow_Language(string name, string language, string expected)
    {
        Assert.Equal(expected, TableNames.Display(name, language));
    }
}
