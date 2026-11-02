using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RestoranYonetim.Data;
using RestoranYonetim.Services.I18n;

namespace RestoranYonetim.Services;

public sealed record LegalLink(string Slug, string Title);

public sealed record LegalPageData(string Title, string Body, DateTime UpdatedAt, bool TurkishOnly = false);

// Footer'daki yasal sayfa linkleri her sayfada gösterildiği için yayındaki sayfaların listesi ve sayfa gövdeleri 60sn önbelleğe alınır.
public static class LegalPageCache
{
    private const string CacheKey = "legal-links";
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);

    public static async Task<IReadOnlyList<LegalLink>> GetPublishedAsync(ApplicationDbContext context, IMemoryCache cache)
    {
        if (cache.TryGetValue(CacheKeys.For(CacheKey), out IReadOnlyList<LegalLink>? cached) && cached != null)
        {
            return cached;
        }

        var pages = await context.LegalPages
            .AsNoTracking()
            .Where(p => p.IsPublished)
            .OrderBy(p => p.Id)
            .ToListAsync();
        await ContentTranslator.ApplyAsync(context, pages);
        IReadOnlyList<LegalLink> links = pages.Select(p => new LegalLink(p.Slug, p.Title)).ToList();

        cache.Set(CacheKeys.For(CacheKey), links, Ttl);
        return links;
    }

    public static string PageKey(string slug) => "legal-page:" + slug;

    // Yayında olmayan sayfa için null döner; null da önbelleğe alınır.
    public static Task<LegalPageData?> GetPageAsync(ApplicationDbContext context, IMemoryCache cache, string slug) =>
        cache.GetOrCreateAsync(CacheKeys.For(PageKey(slug)), async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = Ttl;
            var page = await context.LegalPages.AsNoTracking().FirstOrDefaultAsync(p => p.Slug == slug && p.IsPublished);
            if (page == null)
            {
                return null;
            }
            // Gövdenin çevirisi yoksa Türkçe metin "yalnızca Türkçe" notuyla gösterilir.
            var turkishBody = page.Body;
            await ContentTranslator.ApplyAsync(context, page);
            return new LegalPageData(page.Title, page.Body, page.UpdatedAt, SiteLanguages.Current != SiteLanguages.Default && page.Body == turkishBody);
        });

    public static void Invalidate(IMemoryCache cache)
    {
        CacheKeys.Remove(cache, CacheKey);
        foreach (var (slug, _, _) in LegalPageTemplates.All)
        {
            CacheKeys.Remove(cache, PageKey(slug));
        }
    }
}
