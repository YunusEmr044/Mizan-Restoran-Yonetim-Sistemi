using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using RestoranYonetim.Data;
using RestoranYonetim.Services;

namespace RestoranYonetim.ViewComponents;

// Müşteri sayfalarının sekme ikonu: restoranın SEO ayarlarındaki ikon; yoksa ürün (Mizan) ikonu değil, nötr bir ikon.
public class SiteIconViewComponent : ViewComponent
{
    public const string NeutralIcon = "/img/site-icon.svg";

    private readonly ApplicationDbContext _context;
    private readonly IMemoryCache _cache;

    public SiteIconViewComponent(ApplicationDbContext context, IMemoryCache cache)
    {
        _context = context;
        _cache = cache;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var url = (await SeoCache.GetSettingsAsync(_context, _cache)).FaviconUrl;
        return View("Default", string.IsNullOrWhiteSpace(url) ? NeutralIcon : url);
    }
}
