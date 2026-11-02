using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using RestoranYonetim.Data;
using RestoranYonetim.Models;
using RestoranYonetim.Services;
using RestoranYonetim.Services.Publishing;

namespace RestoranYonetim.ViewComponents;

public sealed record ConsentViewModel(AnalyticsProvider Provider, string? AnalyticsId, string? ScriptUrl, bool RequiresConsent, bool ShowBanner);

// Çerez şeridi + analytics scriptleri; _PublicLayout'ta </body> öncesi çağrılır.
public class ConsentViewComponent : ViewComponent
{
    private readonly ApplicationDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly ILogger<ConsentViewComponent> _logger;

    public ConsentViewComponent(ApplicationDbContext context, IMemoryCache cache, ILogger<ConsentViewComponent> logger)
    {
        _context = context;
        _cache = cache;
        _logger = logger;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var content = await SiteContentCache.GetAsync(_context, _cache, _logger);
        var provider = content?.AnalyticsProvider ?? AnalyticsProvider.Yok;
        if (AnalyticsSettings.Validate(provider, content?.AnalyticsId, content?.AnalyticsScriptUrl) != null)
        {
            provider = AnalyticsProvider.Yok;
        }

        HttpContext.Items[ContentSecurityPolicyBuilder.AnalyticsItemKey] = new AnalyticsCsp(provider, content?.AnalyticsScriptUrl?.Trim());

        var state = ConsentCookie.Parse(Request.Cookies[ConsentCookie.Name]);
        var model = new ConsentViewModel(
            provider,
            content?.AnalyticsId?.Trim(),
            content?.AnalyticsScriptUrl?.Trim(),
            AnalyticsSettings.RequiresConsent(provider),
            ConsentCookie.ShouldShowBanner(provider, state));

        return View(model);
    }
}
