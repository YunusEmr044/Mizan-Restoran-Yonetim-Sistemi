using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RestoranYonetim.Data;
using RestoranYonetim.Models;
using RestoranYonetim.Security;
using RestoranYonetim.Services;
using RestoranYonetim.Services.Publishing;

namespace RestoranYonetim.Controllers;

public sealed record LegalPageView(string Title, string Html, DateTime UpdatedAt, bool TurkishOnly = false);

// KVKK, gizlilik, çerez politikası ve kullanım koşulları - içerik panelden (Site → Yasal Sayfalar) yönetilir.
public class LegalController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly IConfiguration _configuration;
    private readonly ILogger<LegalController> _logger;

    public LegalController(ApplicationDbContext context, IMemoryCache cache, IConfiguration configuration, ILogger<LegalController> logger)
    {
        _context = context;
        _cache = cache;
        _configuration = configuration;
        _logger = logger;
    }

    [HttpGet("/kvkk")]
    public Task<IActionResult> Kvkk() => ShowAsync(LegalPageTemplates.Slugs.Kvkk);

    [HttpGet("/gizlilik")]
    public Task<IActionResult> Gizlilik() => ShowAsync(LegalPageTemplates.Slugs.Gizlilik);

    [HttpGet("/cerez-politikasi")]
    public Task<IActionResult> CerezPolitikasi() => ShowAsync(LegalPageTemplates.Slugs.CerezPolitikasi);

    [HttpGet("/kullanim-kosullari")]
    public Task<IActionResult> KullanimKosullari() => ShowAsync(LegalPageTemplates.Slugs.KullanimKosullari);

    private async Task<IActionResult> ShowAsync(string slug)
    {
        var page = await LegalPageCache.GetPageAsync(_context, _cache, slug);
        if (page == null)
        {
            return NotFound();
        }

        var content = (await SiteContentCache.GetAsync(_context, _cache, _logger)) ?? new SiteContent();
        PublicLayoutData.Apply(ViewBag, content);
        await PublicSeo.ApplyAsync(this, _context, _cache, _configuration, content, slug,
            new SeoPageDefinition(slug, page.Title, "/" + slug, page.Title, $"{page.Title} — {content.HeroTitle}"));
        ViewBag.Breadcrumb = new List<(string Name, string? Url)> { ("Ana Sayfa", "/"), (page.Title, null) };

        var values = LegalTemplateValues.From(content, (string)ViewBag.SeoBaseUrl);
        return View("Show", new LegalPageView(page.Title, MarkdownRenderer.ToSafeHtml(LegalTemplate.Fill(page.Body, values)), page.UpdatedAt, page.TurkishOnly));
    }
}
