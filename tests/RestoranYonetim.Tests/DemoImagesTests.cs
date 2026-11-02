using RestoranYonetim.Data;

namespace RestoranYonetim.Tests;

public class DemoImagesTests
{
    internal static string WebRoot()
    {
        // Testler -o ile başka klasöre derlendiğinde çıktı klasörü projenin dışında kalır; kaynak dosyanın yolundan da aranır.
        var root = new[] { AppContext.BaseDirectory, SourceDirectory() }.Select(FindProject).FirstOrDefault(d => d != null);
        return Path.Combine(root ?? throw new DirectoryNotFoundException("RestoranYonetim.csproj bulunamadı."), "wwwroot");
    }

    private static string SourceDirectory([System.Runtime.CompilerServices.CallerFilePath] string path = "") => Path.GetDirectoryName(path) ?? "";

    private static string? FindProject(string start)
    {
        var dir = new DirectoryInfo(start);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "RestoranYonetim.csproj")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName;
    }

    public static IEnumerable<object[]> AllUrls() =>
        DemoImages.MenuItems.Values
            .Concat(DemoImages.Gallery.Select(g => g.Url))
            .Concat(DemoImages.HeroSlides.Select(h => h.Url))
            .Concat([DemoImages.About, DemoImages.ReservationCta, DemoImages.QrPhone])
            .Select(u => new object[] { u });

    [Theory]
    [MemberData(nameof(AllUrls))]
    public void ImageFileExists(string url)
    {
        Assert.StartsWith("/demo/", url);
        Assert.True(File.Exists(Path.Combine(WebRoot(), url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar))), url);
    }

    [Fact]
    public void FeaturedItemsHavePhotos()
    {
        Assert.All(DemoImages.FeaturedMenuItems, name => Assert.True(DemoImages.MenuItems.ContainsKey(name), name));
        Assert.Equal(4, DemoImages.Gallery.Count(g => g.Home));
    }
}
