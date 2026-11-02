using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RestoranYonetim.Data;
using RestoranYonetim.Models;
using RestoranYonetim.Security;
using RestoranYonetim.Services;
using RestoranYonetim.Services.I18n;
using RestoranYonetim.Services.Publishing;

namespace RestoranYonetim.Controllers;

// Herkese açık restoran sitesi: ana sayfa, menü, hakkımızda, galeri, rezervasyon, iletişim. Giriş gerektirmez.
public class HomeController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<HomeController> _logger;
    private readonly RealtimeNotifier _realtime;
    private readonly IMemoryCache _cache;
    private readonly IConfiguration _configuration;
    private readonly TextLocalizer _t;

    public HomeController(ApplicationDbContext context, ILogger<HomeController> logger, RealtimeNotifier realtime, IMemoryCache cache, IConfiguration configuration, TextLocalizer t)
    {
        _context = context;
        _t = t;
        _logger = logger;
        _realtime = realtime;
        _cache = cache;
        _configuration = configuration;
    }

    // Yapısal verideki sayfa adresleri ziyaret edilen dildeki adres olur (ör. /en/menu).
    private async Task<string> ResolveBaseUrlAsync() =>
        (await PublicUrl.ResolveAsync(HttpContext, _context, _cache, _configuration)).TrimEnd('/') + Request.PathBase;

    private Task ApplySeoAsync(string pageKey, SiteContent content) =>
        PublicSeo.ApplyAsync(this, _context, _cache, _configuration, content, pageKey);

    private async Task<SiteContent> GetSiteContentAsync()
    {
        var content = (await SiteContentCache.GetAsync(_context, _cache, _logger)) ?? new SiteContent();
        PublicLayoutData.Apply(ViewBag, content);
        return content;
    }

    public async Task<IActionResult> Index()
    {
        var content = await GetSiteContentAsync();
        await ApplySeoAsync(SeoPageCatalog.Home, content);
        ViewBag.GalleryPreview = await HomePreviewCache.GetGalleryPreviewAsync(_context, _cache);
        ViewBag.ActiveCampaigns = await HomePreviewCache.GetActiveCampaignsAsync(_context, _cache);
        ViewBag.Testimonials = await HomePreviewCache.GetTestimonialsAsync(_context, _cache);

        ViewBag.HeroSlides = await HomePreviewCache.GetHeroSlidesAsync(_context, _cache);
        ViewBag.Stats = await HomePreviewCache.GetStatsAsync(_context, _cache);
        ViewBag.Features = await HomePreviewCache.GetFeaturesAsync(_context, _cache);

        // Admin hiç ürünü "Ana Sayfada Öne Çıkar" ile işaretlemediyse görseli olan en yeni ürünlere düşülür.
        var featuredMenu = await HomePreviewCache.GetFeaturedMenuAsync(_context, _cache);
        if (featuredMenu.Count == 0)
        {
            var menu = await MenuCache.GetAsync(_context, _cache);
            featuredMenu = menu.Categories
                .SelectMany(c => c.MenuItems)
                .Where(m => m.IsAvailable && !string.IsNullOrEmpty(m.ImageUrl))
                .OrderByDescending(m => m.Id)
                .Take(4)
                .ToList();
        }
        ViewBag.FeaturedMenu = featuredMenu;

        ViewBag.ChefSpecial = await HomePreviewCache.GetChefSpecialAsync(_context, _cache);

        var enabledSections = await HomePreviewCache.GetEnabledSectionsAsync(_context, _cache);
        ViewBag.EnabledSectionKeys = enabledSections.Select(s => s.Key).ToHashSet();
        ViewBag.SectionOrder = enabledSections.Select(s => s.Key).ToList();

        var workingHours = await HomePreviewCache.GetWorkingHoursAsync(_context, _cache);
        ViewBag.WorkingHoursDays = workingHours;
        ViewBag.WorkingHoursStatus = WorkingHoursStatus.Compute(workingHours, DateTime.Now, _t);

        // Rating gerçek yayınlanmış yorumlardan hesaplanır - sahte puan asla üretilmez (bkz. ComputeRating).
        var seoSettings = await SeoCache.GetSettingsAsync(_context, _cache);
        var baseUrl = await ResolveBaseUrlAsync();
        var publishedFeedback = await HomePreviewCache.GetAllPublishedFeedbackAsync(_context, _cache);
        var rating = StructuredDataService.ComputeRating(publishedFeedback);
        var restaurantNode = StructuredDataService.BuildRestaurant(content, seoSettings, workingHours, rating, baseUrl);
        var reviews = StructuredDataService.BuildReviews(publishedFeedback, StructuredDataService.RestaurantId(baseUrl), _t["Misafir"]);
        if (reviews.Count > 0)
        {
            restaurantNode["review"] = reviews;
        }
        ViewBag.StructuredDataHtml = StructuredDataService.ToScriptTag(new object[] { restaurantNode });

        return View(content);
    }

    [HttpGet("/menu")]
    public async Task<IActionResult> Menu()
    {
        var content = await GetSiteContentAsync();
        await ApplySeoAsync(SeoPageCatalog.Menu, content);
        var menu = await MenuCache.GetAsync(_context, _cache);

        var baseUrl = await ResolveBaseUrlAsync();
        var restaurantId = StructuredDataService.RestaurantId(baseUrl);
        var menuNode = StructuredDataService.BuildMenu(menu.Categories, baseUrl, restaurantId, _t["Menü"]);
        var breadcrumb = StructuredDataService.BuildBreadcrumbList(new[]
        {
            (_t["Ana Sayfa"], baseUrl.TrimEnd('/') + "/"),
            (_t["Menü"], baseUrl.TrimEnd('/') + "/menu")
        });
        ViewBag.StructuredDataHtml = StructuredDataService.ToScriptTag(new object[] { menuNode, breadcrumb });
        ViewBag.Breadcrumb = new List<(string Name, string? Url)> { ("Ana Sayfa", "/"), ("Menü", null) };

        return View(menu.Categories);
    }

    [HttpGet("/hakkimizda")]
    public async Task<IActionResult> Hakkimizda()
    {
        var content = await GetSiteContentAsync();
        await ApplySeoAsync(SeoPageCatalog.About, content);
        ViewBag.Testimonials = await HomePreviewCache.GetTestimonialsAsync(_context, _cache);

        var baseUrl = await ResolveBaseUrlAsync();
        var breadcrumb = StructuredDataService.BuildBreadcrumbList(new[]
        {
            (_t["Ana Sayfa"], baseUrl.TrimEnd('/') + "/"),
            (_t["Hakkımızda"], baseUrl.TrimEnd('/') + "/hakkimizda")
        });
        ViewBag.StructuredDataHtml = StructuredDataService.ToScriptTag(new object[] { breadcrumb });
        ViewBag.Breadcrumb = new List<(string Name, string? Url)> { ("Ana Sayfa", "/"), ("Hakkımızda", null) };

        return View(content);
    }

    [HttpGet("/galeri")]
    public async Task<IActionResult> Galeri()
    {
        var content = await GetSiteContentAsync();
        await ApplySeoAsync(SeoPageCatalog.Gallery, content);
        var images = await _context.GalleryImages.AsNoTracking().OrderBy(g => g.DisplayOrder).ToListAsync();
        await ContentTranslator.ApplyAsync(_context, images);

        var baseUrl = await ResolveBaseUrlAsync();
        var breadcrumb = StructuredDataService.BuildBreadcrumbList(new[]
        {
            (_t["Ana Sayfa"], baseUrl.TrimEnd('/') + "/"),
            (_t["Galeri"], baseUrl.TrimEnd('/') + "/galeri")
        });
        ViewBag.StructuredDataHtml = StructuredDataService.ToScriptTag(new object[] { breadcrumb });
        ViewBag.Breadcrumb = new List<(string Name, string? Url)> { ("Ana Sayfa", "/"), ("Galeri", null) };

        return View(images);
    }

    [HttpGet("/iletisim")]
    public async Task<IActionResult> Iletisim()
    {
        var content = await GetSiteContentAsync();
        await ApplySeoAsync(SeoPageCatalog.Contact, content);
        var workingHours = await HomePreviewCache.GetWorkingHoursAsync(_context, _cache);
        ViewBag.WorkingHoursDays = workingHours;
        ViewBag.WorkingHoursStatus = WorkingHoursStatus.Compute(workingHours, DateTime.Now, _t);

        var seoSettings = await SeoCache.GetSettingsAsync(_context, _cache);
        var baseUrl = await ResolveBaseUrlAsync();
        var publishedFeedback = await HomePreviewCache.GetAllPublishedFeedbackAsync(_context, _cache);
        var rating = StructuredDataService.ComputeRating(publishedFeedback);
        var restaurantNode = StructuredDataService.BuildRestaurant(content, seoSettings, workingHours, rating, baseUrl);
        var breadcrumb = StructuredDataService.BuildBreadcrumbList(new[]
        {
            (_t["Ana Sayfa"], baseUrl.TrimEnd('/') + "/"),
            (_t["İletişim"], baseUrl.TrimEnd('/') + "/iletisim")
        });
        ViewBag.StructuredDataHtml = StructuredDataService.ToScriptTag(new object[] { restaurantNode, breadcrumb });
        ViewBag.Breadcrumb = new List<(string Name, string? Url)> { ("Ana Sayfa", "/"), ("İletişim", null) };

        return View(content);
    }

    // SSS yönetimi: Areas/Admin/Controllers/FaqController.cs.
    [HttpGet("/sss")]
    public async Task<IActionResult> Sss()
    {
        var content = await GetSiteContentAsync();
        await ApplySeoAsync(SeoPageCatalog.Faq, content);
        var faqs = await SeoCache.GetActiveFaqsAsync(_context, _cache);

        var baseUrl = await ResolveBaseUrlAsync();
        var breadcrumb = StructuredDataService.BuildBreadcrumbList(new[]
        {
            (_t["Ana Sayfa"], baseUrl.TrimEnd('/') + "/"),
            (_t["Sıkça Sorulan Sorular"], baseUrl.TrimEnd('/') + "/sss")
        });
        var nodes = new List<object> { breadcrumb };
        if (faqs.Count > 0)
        {
            nodes.Add(StructuredDataService.BuildFaqPage(faqs));
        }
        ViewBag.StructuredDataHtml = StructuredDataService.ToScriptTag(nodes);
        ViewBag.Breadcrumb = new List<(string Name, string? Url)> { ("Ana Sayfa", "/"), ("Sıkça Sorulan Sorular", null) };

        return View(faqs);
    }

    [HttpGet("/rezervasyon")]
    public async Task<IActionResult> Rezervasyon()
    {
        var content = await GetSiteContentAsync();
        await ApplySeoAsync(SeoPageCatalog.Reservation, content);
        var baseUrl = await ResolveBaseUrlAsync();
        var breadcrumb = StructuredDataService.BuildBreadcrumbList(new[]
        {
            (_t["Ana Sayfa"], baseUrl.TrimEnd('/') + "/"),
            (_t["Rezervasyon"], baseUrl.TrimEnd('/') + "/rezervasyon")
        });
        ViewBag.StructuredDataHtml = StructuredDataService.ToScriptTag(new object[] { breadcrumb });
        ViewBag.Breadcrumb = new List<(string Name, string? Url)> { ("Ana Sayfa", "/"), ("Rezervasyon", null) };

        if (!content.ReservationEnabled)
        {
            // Form kapalıyken noindex - SeoController.Sitemap da bu sayfayı bu durumda site haritasından çıkarır.
            ViewBag.SeoNoIndex = true;
            return View("RezervasyonKapali");
        }
        return View(new Reservation { Date = DateTime.Today, Time = new TimeSpan(19, 0, 0) });
    }

    [HttpPost("/rezervasyon")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("public-endpoints")]
    public async Task<IActionResult> Rezervasyon([Bind("Name,Phone,Email,Date,Time,PartySize,Note")] Reservation model)
    {
        var content = await GetSiteContentAsync();
        if (!content.ReservationEnabled)
        {
            return View("RezervasyonKapali");
        }

        if (model.Date.Date + model.Time < DateTime.Now)
        {
            ModelState.AddModelError(nameof(Reservation.Date), _t["Geçmiş bir tarih/saat için rezervasyon yapılamaz."]);
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        model.Status = ReservationStatus.Beklemede;
        model.CreatedAt = DateTime.Now;
        _context.Reservations.Add(model);
        _context.Notifications.Add(new Notification
        {
            Message = $"Yeni rezervasyon talebi: {model.Name} - {model.Date:dd.MM.yyyy} {model.Time:hh\\:mm} ({model.PartySize} kişi)",
            Link = "/Admin/Reservations",
            RequiredPermission = Permissions.Reservations.View
        });
        await _context.SaveChangesAsync();
        await _realtime.NotifyAsync("notifications", "reservation", $"Yeni rezervasyon: {model.Name}");

        TempData["ReservationSummary"] = JsonSerializer.Serialize(new ReservationThanksView(model.Name, model.Date.ToString("dd.MM.yyyy"), model.Time.ToString(@"hh\:mm"), model.PartySize));
        return RedirectToAction(nameof(RezervasyonTesekkurler));
    }

    [HttpGet("/rezervasyon/tesekkurler")]
    public async Task<IActionResult> RezervasyonTesekkurler()
    {
        var json = TempData["ReservationSummary"] as string;
        return await ThanksAsync("/rezervasyon/tesekkurler", new ThanksView(
            "bi-calendar-check",
            _t["Rezervasyon talebiniz alındı"],
            _t["Talebinizi en kısa sürede değerlendirip telefonla sizinle iletişime geçeceğiz. Rezervasyonunuz bu onayla kesinleşir."],
            json == null ? null : JsonSerializer.Deserialize<ReservationThanksView>(json)));
    }

    [HttpGet("/degerlendirme")]
    public async Task<IActionResult> Degerlendirme(int? masa)
    {
        await GetSiteContentAsync();
        var feedback = new Feedback { Rating = 5 };
        if (masa.HasValue)
        {
            feedback.TableId = masa;
        }
        return View(feedback);
    }

    // ÖNEMLİ: bu uç girişsiz/herkese açık - rate limit olmadan CSRF token alınıp sahte değerlendirme spam'i yapılabilir.
    [HttpPost("/degerlendirme")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("public-endpoints")]
    public async Task<IActionResult> Degerlendirme([Bind("Name,Rating,Comment,TableId")] Feedback model)
    {
        await GetSiteContentAsync();

        if (model.TableId.HasValue && !await _context.RestaurantTables.AnyAsync(t => t.Id == model.TableId))
        {
            model.TableId = null;
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        model.CreatedAt = DateTime.Now;
        _context.Feedbacks.Add(model);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(DegerlendirmeTesekkurler));
    }

    [HttpGet("/degerlendirme/tesekkurler")]
    public Task<IActionResult> DegerlendirmeTesekkurler() =>
        ThanksAsync("/degerlendirme/tesekkurler", new ThanksView(
            "bi-heart",
            _t["Değerlendirmeniz için teşekkürler"],
            _t["Görüşleriniz hizmetimizi geliştirmemize yardımcı oluyor. Yorumunuz incelendikten sonra sitemizde yayımlanabilir."],
            null));

    private async Task<IActionResult> ThanksAsync(string path, ThanksView model)
    {
        var content = await GetSiteContentAsync();
        await PublicSeo.ApplyAsync(this, _context, _cache, _configuration, content, path,
            new SeoPageDefinition(path, "Teşekkürler", path, "Teşekkürler", model.Heading));
        ViewBag.SeoNoIndex = true;
        return View("Tesekkurler", model);
    }

    // Hem UseExceptionHandler (500'ler) hem UseStatusCodePagesWithReExecute (404/403, ?statusCode=xxx) buraya yönlenir.
    public async Task<IActionResult> Error(int? statusCode)
    {
        ViewBag.StatusCode = statusCode;
        try
        {
            await GetSiteContentAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Hata sayfası için site içeriği yüklenemedi.");
        }
        return View();
    }
}

public sealed record ReservationThanksView(string Name, string Date, string Time, int PartySize);

public sealed record ThanksView(string Icon, string Heading, string Text, ReservationThanksView? Reservation);
