using System.Text;
using System.Xml.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using RestoranYonetim.Data;
using RestoranYonetim.Models;
using RestoranYonetim.Security;
using RestoranYonetim.Services;
using RestoranYonetim.Services.I18n;
using RestoranYonetim.Services.Publishing;

namespace RestoranYonetim.Controllers;

// /sitemap.xml ve /robots.txt - statik dosya değil, veritabanındaki admin ayarlarından (SeoSettings/PageSeo) dinamik üretilir.
public class SeoController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly ILogger<SeoController> _logger;
    private readonly IConfiguration _configuration;

    public SeoController(ApplicationDbContext context, IMemoryCache cache, ILogger<SeoController> logger, IConfiguration configuration)
    {
        _context = context;
        _cache = cache;
        _logger = logger;
        _configuration = configuration;
    }

    private Task<string> ResolveBaseUrlAsync() => PublicUrl.ResolveAsync(HttpContext, _context, _cache, _configuration);

    [HttpGet("/sitemap.xml")]
    public async Task<IActionResult> Sitemap()
    {
        var settings = await SeoCache.GetSettingsAsync(_context, _cache);
        if (!settings.SitemapEnabled)
        {
            return NotFound();
        }

        var siteContent = await SiteContentCache.GetAsync(_context, _cache, _logger);
        var pages = await SeoCache.GetPagesAsync(_context, _cache);
        var baseUrl = await ResolveBaseUrlAsync();

        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
        XNamespace xhtml = "http://www.w3.org/1999/xhtml";
        var urlset = new XElement(ns + "urlset", new XAttribute(XNamespace.Xmlns + "xhtml", xhtml));

        // Her sayfa her dildeki adresiyle ayrı listelenir; diller birbirine hreflang ile bağlanır.
        void AddPage(string path, params XElement[] extra)
        {
            foreach (var lang in SiteLanguages.All)
            {
                var url = new XElement(ns + "url", new XElement(ns + "loc", baseUrl + SiteLanguages.PathFor(lang, path, QueryString.Empty)));
                foreach (var alt in SiteLanguages.All)
                {
                    url.Add(new XElement(xhtml + "link", new XAttribute("rel", "alternate"), new XAttribute("hreflang", alt),
                        new XAttribute("href", baseUrl + SiteLanguages.PathFor(alt, path, QueryString.Empty))));
                }
                url.Add(extra.Select(e => new XElement(e)));
                urlset.Add(url);
            }
        }

        foreach (var page in SeoPageCatalog.All)
        {
            // Rezervasyon formu kapalıysa o sayfa arama motoruna önerilmemeli.
            if (page.Key == SeoPageCatalog.Reservation && siteContent?.ReservationEnabled == false)
            {
                continue;
            }

            // noindex işaretli sayfalar site haritasından da çıkarılır - çelişkili sinyal olmasın diye.
            if (pages.TryGetValue(page.Key, out var pageSeo) && pageSeo.NoIndex)
            {
                continue;
            }

            AddPage(page.Path);
        }

        foreach (var link in await LegalPageCache.GetPublishedAsync(_context, _cache))
        {
            AddPage("/" + link.Slug, new XElement(ns + "changefreq", "yearly"), new XElement(ns + "priority", "0.3"));
        }

        var declaration = new XDeclaration("1.0", "utf-8", null);
        var xml = declaration.ToString() + Environment.NewLine + urlset.ToString();
        return Content(xml, "application/xml", Encoding.UTF8);
    }

    [HttpGet("/llms.txt")]
    public async Task<IActionResult> LlmsTxt()
    {
        var settings = await SeoCache.GetSettingsAsync(_context, _cache);
        if (!settings.IndexingEnabled)
        {
            return NotFound();
        }

        var content = await SiteContentCache.GetAsync(_context, _cache, _logger) ?? new SiteContent();
        var menu = await MenuCache.GetAsync(_context, _cache);
        var text = LlmsTxtBuilder.Build(new LlmsTxtInput(
            await ResolveBaseUrlAsync(),
            content.HeroTitle,
            content.SeoDescription ?? content.HeroSubtitle,
            content.ContactAddress,
            content.ContactPhone,
            WorkingHoursStatus.Summarize(await HomePreviewCache.GetWorkingHoursAsync(_context, _cache)) ?? content.WorkingHours,
            SeoPageCatalog.All
                .Where(p => p.Key != SeoPageCatalog.Home && (p.Key != SeoPageCatalog.Reservation || content.ReservationEnabled))
                .Select(p => new LlmsTxtPage(p.Label, p.Path, p.DefaultDescription))
                .ToList(),
            menu.Categories.Select(c => c.Name).ToList()));

        return Content(text, "text/plain; charset=utf-8");
    }

    [HttpGet("/robots.txt")]
    public async Task<IActionResult> Robots()
    {
        var settings = await SeoCache.GetSettingsAsync(_context, _cache);
        var baseUrl = await ResolveBaseUrlAsync();
        var sb = new StringBuilder();

        if (!settings.IndexingEnabled)
        {
            sb.AppendLine("User-agent: *");
            sb.AppendLine("Disallow: /");
            return Content(sb.ToString(), "text/plain", Encoding.UTF8);
        }

        sb.AppendLine("User-agent: *");
        // /menu/ (sondaki "/" ile) sadece masaya özel QR sipariş alt yollarını kapsar - public /menu sayfası etkilenmez.
        sb.AppendLine("Disallow: /Admin/");
        sb.AppendLine("Disallow: /admin");
        sb.AppendLine("Disallow: /menu/");
        sb.AppendLine("Disallow: /Staff/");
        sb.AppendLine("Disallow: /Bar/");
        sb.AppendLine("Disallow: /Mutfak/");
        sb.AppendLine("Disallow: /Kasa/");
        sb.AppendLine("Disallow: /Notifications/");
        sb.AppendLine("Disallow: /hubs/");

        if (!string.IsNullOrWhiteSpace(settings.RobotsExtraRules))
        {
            foreach (var rawLine in settings.RobotsExtraRules.Replace("\r\n", "\n").Split('\n'))
            {
                var line = rawLine.Trim();
                if (!string.IsNullOrEmpty(line))
                {
                    sb.AppendLine(line);
                }
            }
        }

        if (settings.SitemapEnabled)
        {
            sb.AppendLine();
            sb.AppendLine($"Sitemap: {baseUrl}/sitemap.xml");
        }

        return Content(sb.ToString(), "text/plain", Encoding.UTF8);
    }
}
