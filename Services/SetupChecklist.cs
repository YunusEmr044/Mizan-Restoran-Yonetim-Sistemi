using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RestoranYonetim.Data;

namespace RestoranYonetim.Services;

public sealed record SetupSnapshot(string? HeroTitle, string? LogoUrl, string? FaviconUrl, string? Phone, string? Address,
    bool PublicBaseUrlSet, int Tables, int MenuItems);

public sealed record SetupStep(string Key, string Title, string Hint, bool Done);

// Canlıya geçişte restoranın doldurması gereken adımlar; durum veritabanındaki gerçek değerlerden çıkarılır.
public static partial class SetupChecklist
{
    public static bool Filled(string? value) =>
        !string.IsNullOrWhiteSpace(value) && !DemoDataSeeder.PlaceholderTexts.Contains(value.Trim());

    public static IReadOnlyList<SetupStep> Build(SetupSnapshot s) => new[]
    {
        new SetupStep("name", "Restoran adı", "Sitede ve sekmede görünen ad (Site İçeriği → Ana Başlık).", Filled(s.HeroTitle)),
        new SetupStep("logo", "Logo ve sekme ikonu", "Logo Site İçeriği'nden, sekme ikonu SEO Ayarları'ndan yüklenir.", Filled(s.LogoUrl) && Filled(s.FaviconUrl)),
        new SetupStep("contact", "İletişim bilgileri", "Telefon ve adres; yasal sayfalar ve fişler de bunları kullanır.", Filled(s.Phone) && Filled(s.Address)),
        new SetupStep("tables", "Masalar", "Restoran, şube, alan ve masalar; her masanın kendi QR kodu olur.", s.Tables > 0),
        new SetupStep("menu", "Menü", "Kategoriler ve ürünler.", s.MenuItems > 0),
        new SetupStep("url", "Site adresi", "QR kodlarının açacağı adres (Ayarlar → Site Adresi).", s.PublicBaseUrlSet)
    };

    public static bool IsComplete(IReadOnlyList<SetupStep> steps) => steps.All(s => s.Done);

    [GeneratedRegex(@"^Masa (\d+)$")]
    private static partial Regex TableNumber();

    public static IReadOnlyList<string> NewTableNames(IEnumerable<string> existing, int count)
    {
        var last = existing.Select(n => TableNumber().Match(n.Trim())).Where(m => m.Success)
            .Select(m => int.Parse(m.Groups[1].Value)).DefaultIfEmpty(0).Max();
        return Enumerable.Range(last + 1, count).Select(n => $"Masa {n}").ToList();
    }
}

public static class SetupChecklistLoader
{
    public static async Task<IReadOnlyList<SetupStep>> LoadAsync(ApplicationDbContext context, IMemoryCache cache, IConfiguration configuration)
    {
        var content = await SiteContentCache.GetAsync(context, cache);
        var seo = await SeoCache.GetSettingsAsync(context, cache);
        return SetupChecklist.Build(new SetupSnapshot(content?.HeroTitle, content?.LogoImageUrl, seo.FaviconUrl, content?.ContactPhone, content?.ContactAddress,
            PublicUrl.TryNormalize(content?.PublicBaseUrl, out _) || PublicUrl.TryNormalize(configuration["PublicBaseUrl"], out _),
            await context.RestaurantTables.CountAsync(),
            await context.MenuItems.CountAsync()));
    }
}
