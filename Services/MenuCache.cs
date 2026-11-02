using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RestoranYonetim.Data;
using RestoranYonetim.Services.I18n;
using RestoranYonetim.Models;

namespace RestoranYonetim.Services;

// SiteContentCache ile aynı desen (kısa TTL + thundering-herd kilidi + admin değişikliğinde Invalidate).
// ÖNEMLİ: TTL burada kasıtlı olarak SiteContentCache'ten (60sn) daha kısa (15sn), çünkü
// MenuItem.IsAvailable ("Tükendi") müşteri deneyimini doğrudan etkiler.
public static class MenuCache
{
    private const string CacheKey = "menu-full";
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(15);
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public record MenuSnapshot(List<Category> Categories, List<MenuItemOption> Options);

    public static async Task<MenuSnapshot> GetAsync(ApplicationDbContext context, IMemoryCache cache)
    {
        if (cache.TryGetValue(CacheKeys.For(CacheKey), out MenuSnapshot? cached) && cached != null)
        {
            return cached;
        }

        await Gate.WaitAsync();
        try
        {
            if (cache.TryGetValue(CacheKeys.For(CacheKey), out cached) && cached != null)
            {
                return cached;
            }

            var categories = await context.Categories
                .AsNoTracking()
                .Include(c => c.MenuItems.Where(m => m.IsAvailable))
                .OrderBy(c => c.DisplayOrder)
                .ToListAsync();

            var itemIds = categories.SelectMany(c => c.MenuItems).Select(m => m.Id).ToList();
            var options = await context.MenuItemOptions
                .AsNoTracking()
                .Where(o => o.IsAvailable && itemIds.Contains(o.MenuItemId))
                .ToListAsync();

            await ContentTranslator.ApplyAsync(context, categories.Cast<object>().Concat(categories.SelectMany(c => c.MenuItems)).Concat(options));
            var snapshot = new MenuSnapshot(categories, options);
            cache.Set(CacheKeys.For(CacheKey), snapshot, Ttl);
            return snapshot;
        }
        finally
        {
            Gate.Release();
        }
    }

    public static void Invalidate(IMemoryCache cache) => CacheKeys.Remove(cache, CacheKey);
}
