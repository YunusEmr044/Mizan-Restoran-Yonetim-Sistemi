using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RestoranYonetim.Data;
using RestoranYonetim.Models;
using RestoranYonetim.Security;
using RestoranYonetim.Services;

namespace RestoranYonetim.Controllers;

// Herkese açık restoran sitesi: ana sayfa, menü, hakkımızda, galeri, rezervasyon, iletişim. Giriş gerektirmez.
public class HomeController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<HomeController> _logger;
    private readonly RealtimeNotifier _realtime;
    private readonly IMemoryCache _cache;
    private readonly IConfiguration _configuration;

    public HomeController(ApplicationDbContext context, ILogger<HomeController> logger, RealtimeNotifier realtime, IMemoryCache cache, IConfiguration configuration)
    {
        _context = context;
        _logger = logger;
        _realtime = realtime;
        _cache = cache;
        _configuration = configuration;
    }

    private Task<string> ResolveBaseUrlAsync() => PublicUrl.ResolveAsync(HttpContext, _context, _cache, _configuration);

    // SEO alanlarını PageSeo → SeoSettings → kod içi varsayılan sırasıyla çözüp ViewBag'e yazar.
    private async Task ApplySeoAsync(string pageKey, SiteContent content)
    {
        var seoSettings = await SeoCache.GetSettingsAsync(_context, _cache);
        var pageSeo = await SeoCache.GetPageAsync(_context, _cache, pageKey);
        var pageDef = SeoPageCatalog.Find(pageKey);
        var baseUrl = await ResolveBaseUrlAsync();
        var isHome = pageKey == SeoPageCatalog.Home;

        var resolvedTitle = pageSeo?.Title
            ?? (isHome ? content.SeoTitle : null)
            ?? pageDef?.DefaultTitle
            ?? content.HeroTitle;

        var fullTitle = isHome ? resolvedTitle : $"{resolvedTitle} | {content.HeroTitle}";

        var resolvedDescription = pageSeo?.MetaDescription
            ?? (isHome ? content.SeoDescription : null)
            ?? pageDef?.DefaultDescription
            ?? seoSettings.DefaultMetaDescription;

        var canonical = !string.IsNullOrWhiteSpace(pageSeo?.CanonicalUrl)
            ? StructuredDataService.AbsoluteUrl(baseUrl, pageSeo!.CanonicalUrl)
            : baseUrl.TrimEnd('/') + (pageDef?.Path ?? "/");

        var ogImage = pageSeo?.OgImageUrl ?? seoSettings.DefaultOgImageUrl ?? content.HeroImageUrl ?? content.LogoImageUrl;

        ViewBag.SeoPageTitle = fullTitle;
        ViewBag.SeoMetaDescription = resolvedDescription;
        ViewBag.SeoCanonicalUrl = canonical;
        ViewBag.SeoNoIndex = !seoSettings.IndexingEnabled || (pageSeo?.NoIndex ?? false);
        ViewBag.SeoNoFollow = pageSeo?.NoFollow ?? false;
        ViewBag.SeoOgTitle = pageSeo?.OgTitle ?? fullTitle;
        ViewBag.SeoOgDescription = pageSeo?.OgDescription ?? resolvedDescription;
        ViewBag.SeoOgImageUrl = string.IsNullOrWhiteSpace(ogImage) ? null : StructuredDataService.AbsoluteUrl(baseUrl, ogImage);
        ViewBag.SeoFaviconUrl = seoSettings.FaviconUrl;
        ViewBag.SeoGoogleVerification = seoSettings.GoogleSiteVerification;
        ViewBag.SeoBingVerification = seoSettings.BingSiteVerification;
        ViewBag.SeoTwitterHandle = seoSettings.TwitterHandle;
        ViewBag.SeoBaseUrl = baseUrl;
        ViewBag.SeoRestaurantId = StructuredDataService.RestaurantId(baseUrl);
    }

    private async Task<SiteContent> GetSiteContentAsync()
    {
        var content = (await SiteContentCache.GetAsync(_context, _cache, _logger)) ?? new SiteContent();
        ViewBag.RestaurantName = content.HeroTitle;
        ViewBag.LogoImageUrl = content.LogoImageUrl;
        ViewBag.SeoTitle = content.SeoTitle;
        ViewBag.SeoDescription = content.SeoDescription;
        ViewBag.ContactPhone = content.ContactPhone;
        ViewBag.ContactEmail = content.ContactEmail;
        ViewBag.ContactAddress = content.ContactAddress;
        ViewBag.WhatsAppNumber = content.WhatsAppNumber;
        ViewBag.WhatsAppDefaultMessage = content.WhatsAppDefaultMessage;
        ViewBag.WorkingHours = content.WorkingHours;
        ViewBag.FooterText = content.FooterText;
        ViewBag.FooterTagline = content.FooterTagline;
        ViewBag.FacebookUrl = content.FacebookUrl;
        ViewBag.InstagramUrl = content.InstagramUrl;
        ViewBag.InstagramHandle = content.InstagramHandle;
        ApplyThemeToViewBag(content);
        return content;
    }

    // Değerler ThemeCatalog.Safe*() ile doğrulanır: DB'de bozuk bir değer olsa bile site geçerli bir görünümle açılır.
    private void ApplyThemeToViewBag(SiteContent content) => ThemeCatalog.ApplyToViewBag(ViewBag, content);

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
        ViewBag.WorkingHoursStatus = WorkingHoursStatus.Compute(workingHours, DateTime.Now);

        // Rating gerçek yayınlanmış yorumlardan hesaplanır - sahte puan asla üretilmez (bkz. ComputeRating).
        var seoSettings = await SeoCache.GetSettingsAsync(_context, _cache);
        var baseUrl = await ResolveBaseUrlAsync();
        var publishedFeedback = await HomePreviewCache.GetAllPublishedFeedbackAsync(_context, _cache);
        var rating = StructuredDataService.ComputeRating(publishedFeedback);
        var restaurantNode = StructuredDataService.BuildRestaurant(content, seoSettings, workingHours, rating, baseUrl);
        var reviews = StructuredDataService.BuildReviews(publishedFeedback, StructuredDataService.RestaurantId(baseUrl));
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
        var menuNode = StructuredDataService.BuildMenu(menu.Categories, baseUrl, restaurantId);
        var breadcrumb = StructuredDataService.BuildBreadcrumbList(new[]
        {
            ("Ana Sayfa", baseUrl.TrimEnd('/') + "/"),
            ("Menü", baseUrl.TrimEnd('/') + "/menu")
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
            ("Ana Sayfa", baseUrl.TrimEnd('/') + "/"),
            ("Hakkımızda", baseUrl.TrimEnd('/') + "/hakkimizda")
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

        var baseUrl = await ResolveBaseUrlAsync();
        var breadcrumb = StructuredDataService.BuildBreadcrumbList(new[]
        {
            ("Ana Sayfa", baseUrl.TrimEnd('/') + "/"),
            ("Galeri", baseUrl.TrimEnd('/') + "/galeri")
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
        ViewBag.WorkingHoursStatus = WorkingHoursStatus.Compute(workingHours, DateTime.Now);

        var seoSettings = await SeoCache.GetSettingsAsync(_context, _cache);
        var baseUrl = await ResolveBaseUrlAsync();
        var publishedFeedback = await HomePreviewCache.GetAllPublishedFeedbackAsync(_context, _cache);
        var rating = StructuredDataService.ComputeRating(publishedFeedback);
        var restaurantNode = StructuredDataService.BuildRestaurant(content, seoSettings, workingHours, rating, baseUrl);
        var breadcrumb = StructuredDataService.BuildBreadcrumbList(new[]
        {
            ("Ana Sayfa", baseUrl.TrimEnd('/') + "/"),
            ("İletişim", baseUrl.TrimEnd('/') + "/iletisim")
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
            ("Ana Sayfa", baseUrl.TrimEnd('/') + "/"),
            ("Sıkça Sorulan Sorular", baseUrl.TrimEnd('/') + "/sss")
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
            ("Ana Sayfa", baseUrl.TrimEnd('/') + "/"),
            ("Rezervasyon", baseUrl.TrimEnd('/') + "/rezervasyon")
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
    public async Task<IActionResult> Rezervasyon(Reservation model)
    {
        var content = await GetSiteContentAsync();
        if (!content.ReservationEnabled)
        {
            return View("RezervasyonKapali");
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

        TempData["Info"] = "Rezervasyon talebiniz alındı. En kısa sürede sizinle iletişime geçeceğiz.";
        return RedirectToAction(nameof(Rezervasyon));
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
    public async Task<IActionResult> Degerlendirme(Feedback model)
    {
        await GetSiteContentAsync();
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        model.CreatedAt = DateTime.Now;
        _context.Feedbacks.Add(model);
        await _context.SaveChangesAsync();

        TempData["Info"] = "Değerlendirmeniz için teşekkür ederiz!";
        return RedirectToAction(nameof(Degerlendirme));
    }

    // Hem UseExceptionHandler (500'ler) hem UseStatusCodePagesWithReExecute (404/403, ?statusCode=xxx) buraya yönlenir.
    public IActionResult Error(int? statusCode)
    {
        ViewBag.StatusCode = statusCode;
        return View();
    }
}
