using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using RestoranYonetim.Data;
using RestoranYonetim.Models;

namespace RestoranYonetim.Services;

// SiteContent tek satırlık bir tablo ama her sayfada okunuyordu; 60sn önbelleğe alınır.
// 60sn'lik gecikme risksiz kabul edilir (izin önbelleğinin aksine - orada anında geçersiz kılma gerekli).
public static class SiteContentCache
{
    private const string CacheKey = "site-content";
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);

    // "Cache stampede" kilidi: önbellek boşken/TTL dolduğunda sadece ilk istek veritabanına gider, diğerleri kilidi bekler.
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public static async Task<SiteContent?> GetAsync(ApplicationDbContext context, IMemoryCache cache, ILogger? logger = null)
    {
        if (cache.TryGetValue(CacheKey, out SiteContent? cached))
        {
            return cached;
        }

        await Gate.WaitAsync();
        try
        {
            if (cache.TryGetValue(CacheKey, out cached))
            {
                return cached;
            }

            SiteContent? content;
            try
            {
                content = await context.SiteContents.AsNoTracking().FirstOrDefaultAsync();
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "SiteContent okunamadı, varsayılan içerikle devam ediliyor.");
                content = null;
            }

            cache.Set(CacheKey, content, Ttl);
            return content;
        }
        finally
        {
            Gate.Release();
        }
    }

    public static void Invalidate(IMemoryCache cache) => cache.Remove(CacheKey);
}
