using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using RestoranYonetim.Data;
using RestoranYonetim.Models;
using RestoranYonetim.Services;
using RestoranYonetim.Services.Publishing;

namespace RestoranYonetim.ViewComponents;

public sealed record LegalLinksViewModel(IReadOnlyList<LegalLink> Links, bool ShowCookieSettings);

public class LegalLinksViewComponent : ViewComponent
{
    private readonly ApplicationDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly ILogger<LegalLinksViewComponent> _logger;

    public LegalLinksViewComponent(ApplicationDbContext context, IMemoryCache cache, ILogger<LegalLinksViewComponent> logger)
    {
        _context = context;
        _cache = cache;
        _logger = logger;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        IReadOnlyList<LegalLink> links;
        try
        {
            links = await LegalPageCache.GetPublishedAsync(_context, _cache);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Yasal sayfa linkleri yüklenemedi.");
            links = Array.Empty<LegalLink>();
        }

        var content = await SiteContentCache.GetAsync(_context, _cache, _logger);
        var showCookieSettings = AnalyticsSettings.RequiresConsent(content?.AnalyticsProvider ?? AnalyticsProvider.Yok);
        return View(new LegalLinksViewModel(links, showCookieSettings));
    }
}
