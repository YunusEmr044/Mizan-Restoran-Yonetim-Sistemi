using Microsoft.Extensions.Caching.Memory;
using RestoranYonetim.Data;
using RestoranYonetim.Services;
using RestoranYonetim.Services.I18n;

namespace RestoranYonetim.Tests;

public class LegalPageCacheTests
{
    [Fact]
    public void Invalidate_ClearsEveryLegalPage()
    {
        using var cache = new MemoryCache(new MemoryCacheOptions());
        foreach (var (slug, title, body) in LegalPageTemplates.All)
        {
            foreach (var key in CacheKeys.AllLanguages(LegalPageCache.PageKey(slug)))
            {
                cache.Set(key, new LegalPageData(title, body, DateTime.Now));
            }
        }

        LegalPageCache.Invalidate(cache);

        Assert.All(LegalPageTemplates.All.SelectMany(p => CacheKeys.AllLanguages(LegalPageCache.PageKey(p.Slug))),
            key => Assert.False(cache.TryGetValue(key, out _), key));
    }

    [Fact]
    public void PageKey_IsPerSlug()
    {
        Assert.NotEqual(LegalPageCache.PageKey("kvkk"), LegalPageCache.PageKey("gizlilik"));
    }
}
