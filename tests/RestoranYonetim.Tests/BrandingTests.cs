using RestoranYonetim.Services;

namespace RestoranYonetim.Tests;

// "Mizan" ürünün adıdır; müşteri sitesi restoranın kendi adını, logosunu ve ikonunu taşır.
public class BrandingTests
{
    private static string ProjectRoot() => Directory.GetParent(DemoImagesTests.WebRoot())!.FullName;

    private static string Read(string relative) => File.ReadAllText(Path.Combine(ProjectRoot(), relative));

    private static readonly string[] PublicFolders = { "Views/Home", "Views/Menu", "Views/Legal", "Views/Shared/Components" };

    [Fact]
    public void Product_Name_Is_Mizan()
    {
        Assert.Equal("Mizan", ProductInfo.Name);
    }

    [Fact]
    public void Customer_Pages_Do_Not_Hardcode_Product_Name()
    {
        var files = PublicFolders.SelectMany(f => Directory.GetFiles(Path.Combine(ProjectRoot(), f), "*.cshtml", SearchOption.AllDirectories))
            .Append(Path.Combine(ProjectRoot(), "Views/Shared/_PublicLayout.cshtml"))
            .Append(Path.Combine(ProjectRoot(), "Views/Shared/_CustomerLayout.cshtml"));
        var offenders = files.Where(f => File.ReadAllText(f).Contains("Mizan", StringComparison.Ordinal)).ToList();
        Assert.Empty(offenders);
    }

    [Fact]
    public void Restaurant_Brand_Does_Not_Fall_Back_To_Product_Logo_Or_Icon()
    {
        var layout = Read("Views/Shared/_PublicLayout.cshtml");
        Assert.Equal(1, CountOf(layout, "logo-mark.svg"));
        Assert.DoesNotContain("~/favicon.svg", layout);
        Assert.DoesNotContain("~/favicon.svg", Read("Views/Shared/_CustomerLayout.cshtml"));
    }

    [Fact]
    public void Staff_Screens_Use_Product_Constant()
    {
        foreach (var file in new[] { "Views/Shared/_Layout.cshtml", "Views/Shared/_AuthLayout.cshtml" })
        {
            var text = Read(file);
            Assert.Contains("ProductInfo.Name", text);
            Assert.DoesNotContain("Mizan", text);
        }
    }

    private static int CountOf(string text, string value)
    {
        var count = 0;
        for (var i = text.IndexOf(value, StringComparison.Ordinal); i >= 0; i = text.IndexOf(value, i + 1, StringComparison.Ordinal))
        {
            count++;
        }
        return count;
    }
}
