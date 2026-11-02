using System.Globalization;
using RestoranYonetim.Services.I18n;

namespace RestoranYonetim.Tests;

public class TextLocalizerTests
{
    private static string RepoRoot() => Directory.GetParent(DemoImagesTests.WebRoot())!.FullName;

    private static TextLocalizer Sample() => new(new Dictionary<string, IReadOnlyDictionary<string, string>>
    {
        ["en"] = new Dictionary<string, string> { ["Menüyü İncele"] = "View Menu", ["{0} kişi"] = "{0} guests" }
    });

    [Fact]
    public void Known_Key_Translates_In_En()
    {
        Assert.Equal("View Menu", Sample().Get("Menüyü İncele", "en"));
    }

    [Fact]
    public void Missing_Key_Falls_Back_To_Turkish()
    {
        Assert.Equal("Rezervasyon", Sample().Get("Rezervasyon", "en"));
    }

    [Fact]
    public void Tr_Returns_Key()
    {
        Assert.Equal("Menüyü İncele", Sample().Get("Menüyü İncele", "tr"));
    }

    [Fact]
    public void Format_Arguments_Use_Current_Language()
    {
        var previous = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.CurrentCulture = SiteLanguages.Culture("en");
            Assert.Equal("4 guests", Sample()["{0} kişi", 4]);
        }
        finally
        {
            CultureInfo.CurrentUICulture = CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void KeysUsedIn_Finds_View_And_Controller_Keys()
    {
        var keys = TextLocalizer.KeysUsedIn("<a>@T[\"Menü\"]</a> @T[\"{0} kişi\", n] TempData[\"x\"] = _t[\"Lütfen \\\"seçin\\\"\"];").ToList();

        Assert.Equal(new[] { "Menü", "{0} kişi", "Lütfen \"seçin\"" }, keys);
    }

    public static IEnumerable<string> PublicSources()
    {
        var root = RepoRoot();
        var views = new[] { "Views/Home", "Views/Menu", "Views/Legal", "Views/Shared/Components/Consent", "Views/Shared/Components/LegalLinks" }
            .SelectMany(d => Directory.GetFiles(Path.Combine(root, d), "*.cshtml"));
        var shared = new[] { "_PublicLayout", "_CustomerLayout", "_Testimonials", "_Breadcrumbs", "_MapEmbed", "_LanguageSwitch" }
            .Select(n => Path.Combine(root, "Views/Shared", n + ".cshtml")).Where(File.Exists);
        var controllers = new[] { "HomeController", "MenuController", "LegalController" }
            .Select(n => Path.Combine(root, "Controllers", n + ".cs"));
        return views.Concat(shared).Concat(controllers);
    }

    [Fact]
    public void All_Public_Keys_Have_English()
    {
        var english = TextLocalizer.Parse(File.ReadAllText(Path.Combine(RepoRoot(), TextLocalizer.ResourceFolder, "en.json")));
        var missing = PublicSources().SelectMany(f => TextLocalizer.KeysUsedIn(File.ReadAllText(f))).Distinct()
            .Where(k => !english.ContainsKey(k)).ToList();

        Assert.True(missing.Count == 0, "en.json eksik anahtarlar:\n" + string.Join("\n", missing));
    }

    [Fact]
    public void English_Json_Has_No_Empty_Values()
    {
        var english = TextLocalizer.Parse(File.ReadAllText(Path.Combine(RepoRoot(), TextLocalizer.ResourceFolder, "en.json")));

        Assert.NotEmpty(english);
        Assert.DoesNotContain(english, p => string.IsNullOrWhiteSpace(p.Value));
    }
}
