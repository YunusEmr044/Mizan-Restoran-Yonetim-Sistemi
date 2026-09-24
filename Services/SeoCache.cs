using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RestoranYonetim.Data;
using RestoranYonetim.Models;

namespace RestoranYonetim.Services;

// SiteContentCache/HomePreviewCache ile aynı desen: kısa TTL (60sn) + tek-uçuşlu (single-flight) kilit.
public static class SeoCache
{
    private const string SettingsKey = "seo-settings";
    private const string PagesKey = "seo-pages";
    private const string FaqsKey = "seo-faqs-active";

    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);

    private static readonly SemaphoreSlim SettingsGate = new(1, 1);
    private static readonly SemaphoreSlim PagesGate = new(1, 1);
    private static readonly SemaphoreSlim FaqsGate = new(1, 1);

    public static async Task<SeoSettings> GetSettingsAsync(ApplicationDbContext context, IMemoryCache cache)
    {
        if (cache.TryGetValue(SettingsKey, out SeoSettings? cached) && cached != null)
        {
            return cached;
        }

        await SettingsGate.WaitAsync();
        try
        {
            if (cache.TryGetValue(SettingsKey, out cached) && cached != null)
            {
                return cached;
            }

            var settings = await context.SeoSettingsRows.AsNoTracking().FirstOrDefaultAsync() ?? new SeoSettings();
            cache.Set(SettingsKey, settings, Ttl);
            return settings;
        }
        finally
        {
            SettingsGate.Release();
        }
    }

    public static async Task<Dictionary<string, PageSeo>> GetPagesAsync(ApplicationDbContext context, IMemoryCache cache)
    {
        if (cache.TryGetValue(PagesKey, out Dictionary<string, PageSeo>? cached) && cached != null)
        {
            return cached;
        }

        await PagesGate.WaitAsync();
        try
        {
            if (cache.TryGetValue(PagesKey, out cached) && cached != null)
            {
                return cached;
            }

            var pages = await context.PageSeos.AsNoTracking().ToDictionaryAsync(p => p.PageKey);
            cache.Set(PagesKey, pages, Ttl);
            return pages;
        }
        finally
        {
            PagesGate.Release();
        }
    }

    public static async Task<PageSeo?> GetPageAsync(ApplicationDbContext context, IMemoryCache cache, string pageKey)
    {
        var pages = await GetPagesAsync(context, cache);
        return pages.TryGetValue(pageKey, out var page) ? page : null;
    }

    public static async Task<List<Faq>> GetActiveFaqsAsync(ApplicationDbContext context, IMemoryCache cache)
    {
        if (cache.TryGetValue(FaqsKey, out List<Faq>? cached) && cached != null)
        {
            return cached;
        }

        await FaqsGate.WaitAsync();
        try
        {
            if (cache.TryGetValue(FaqsKey, out cached) && cached != null)
            {
                return cached;
            }

            var faqs = await context.Faqs.AsNoTracking()
                .Where(f => f.IsActive)
                .OrderBy(f => f.DisplayOrder)
                .ToListAsync();
            cache.Set(FaqsKey, faqs, Ttl);
            return faqs;
        }
        finally
        {
            FaqsGate.Release();
        }
    }

    public static void InvalidateSettings(IMemoryCache cache) => cache.Remove(SettingsKey);
    public static void InvalidatePages(IMemoryCache cache) => cache.Remove(PagesKey);
    public static void InvalidateFaqs(IMemoryCache cache) => cache.Remove(FaqsKey);
}
