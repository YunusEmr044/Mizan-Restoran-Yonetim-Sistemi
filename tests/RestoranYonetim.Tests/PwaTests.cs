using System.Text.Json;

namespace RestoranYonetim.Tests;

public class PwaTests
{
    private static JsonElement Manifest() =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(DemoImagesTests.WebRoot(), "manifest.webmanifest"))).RootElement;

    [Fact]
    public void Manifest_Opens_Staff_Entry_Standalone()
    {
        var m = Manifest();

        Assert.Equal("Mizan", m.GetProperty("short_name").GetString());
        Assert.Equal("/admin", m.GetProperty("start_url").GetString());
        Assert.Equal("/", m.GetProperty("scope").GetString());
        Assert.Equal("standalone", m.GetProperty("display").GetString());
    }

    [Theory]
    [InlineData("192x192", "any")]
    [InlineData("512x512", "any")]
    [InlineData("512x512", "maskable")]
    public void Manifest_Icons_Exist_With_Real_Size(string sizes, string purpose)
    {
        var icon = Manifest().GetProperty("icons").EnumerateArray()
            .Single(i => i.GetProperty("sizes").GetString() == sizes && i.GetProperty("purpose").GetString() == purpose);
        var bytes = File.ReadAllBytes(Path.Combine(DemoImagesTests.WebRoot(), icon.GetProperty("src").GetString()!.TrimStart('/')));
        var width = (bytes[16] << 24) | (bytes[17] << 16) | (bytes[18] << 8) | bytes[19];

        Assert.Equal(int.Parse(sizes.Split('x')[0]), width);
    }

    [Fact]
    public void Shortcuts_Point_To_Staff_Screens()
    {
        var urls = Manifest().GetProperty("shortcuts").EnumerateArray().Select(s => s.GetProperty("url").GetString()).ToList();

        Assert.Equal(new[] { "/Staff", "/Mutfak", "/Kasa" }, urls);
    }

    [Fact]
    public void Service_Worker_Precaches_Offline_Page_And_Never_Caches_Navigations()
    {
        var sw = File.ReadAllText(Path.Combine(DemoImagesTests.WebRoot(), "sw.js"));

        Assert.Contains("/offline.html", sw);
        Assert.Contains("request.mode === 'navigate'", sw);
        Assert.True(File.Exists(Path.Combine(DemoImagesTests.WebRoot(), "offline.html")));
    }
}
