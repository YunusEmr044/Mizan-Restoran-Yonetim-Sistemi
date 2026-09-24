using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RestoranYonetim.Data;
using RestoranYonetim.Models;
using RestoranYonetim.Security;
using RestoranYonetim.Services;

namespace RestoranYonetim.Areas.Admin.Controllers;

// SEO admin yönetimi: (1) Index - site geneli ayarlar, (2) Pages/PageEdit - sabit sayfa kataloğu için sayfa bazlı override (bkz. Models/PageSeo.cs).
[Area("Admin")]
[Authorize(Policy = Permissions.SiteContent.Manage)]
public class SeoSettingsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _env;
    private readonly IMemoryCache _cache;
    private readonly AuditService _audit;

    public SeoSettingsController(ApplicationDbContext context, IWebHostEnvironment env, IMemoryCache cache, AuditService audit)
    {
        _context = context;
        _env = env;
        _cache = cache;
        _audit = audit;
    }

    private async Task<SeoSettings> GetOrCreateSettingsAsync()
    {
        var settings = await _context.SeoSettingsRows.FirstOrDefaultAsync();
        if (settings == null)
        {
            settings = new SeoSettings();
            _context.SeoSettingsRows.Add(settings);
            await _context.SaveChangesAsync();
        }
        return settings;
    }

    // ---------------- Site geneli SEO ayarları ----------------

    public async Task<IActionResult> Index()
    {
        ViewBag.SiteActive = "seo";
        ViewBag.LocalBusinessTypes = SeoPageCatalog.LocalBusinessTypes;
        var settings = await GetOrCreateSettingsAsync();
        return View(settings);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(SeoSettings model, IFormFile? faviconImage, IFormFile? ogImage, bool removeFaviconImage = false, bool removeOgImage = false)
    {
        var existing = await GetOrCreateSettingsAsync();
        ViewBag.SiteActive = "seo";
        ViewBag.LocalBusinessTypes = SeoPageCatalog.LocalBusinessTypes;

        if (!ModelState.IsValid)
        {
            model.Id = existing.Id;
            return View(model);
        }

        model.Id = existing.Id;
        model.FaviconUrl = existing.FaviconUrl;
        model.DefaultOgImageUrl = existing.DefaultOgImageUrl;

        var oldFaviconUrl = existing.FaviconUrl;
        var oldOgImageUrl = existing.DefaultOgImageUrl;

        foreach (var (file, label) in new[] { (faviconImage, "Favicon"), (ogImage, "Varsayılan paylaşım görseli") })
        {
            if (file is { Length: > 0 })
            {
                var uploadError = await ImageUploadValidator.ValidateAsync(file);
                if (uploadError != null)
                {
                    model.Id = existing.Id;
                    ModelState.AddModelError(string.Empty, $"{label}: {uploadError}");
                    return View(model);
                }
            }
        }

        if (faviconImage is { Length: > 0 })
        {
            model.FaviconUrl = await SaveImageAsync(faviconImage, "favicon");
        }
        else if (removeFaviconImage)
        {
            model.FaviconUrl = null;
        }
        if (ogImage is { Length: > 0 })
        {
            model.DefaultOgImageUrl = await SaveImageAsync(ogImage, "paylasim-gorseli");
        }
        else if (removeOgImage)
        {
            model.DefaultOgImageUrl = null;
        }

        _context.Entry(existing).CurrentValues.SetValues(model);
        await _context.SaveChangesAsync();
        SeoCache.InvalidateSettings(_cache);

        if (oldFaviconUrl != model.FaviconUrl) DeleteImageFileIfExists(oldFaviconUrl);
        if (oldOgImageUrl != model.DefaultOgImageUrl) DeleteImageFileIfExists(oldOgImageUrl);

        await _audit.LogAsync(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            User.Identity?.Name,
            "SEO ayarları güncellendi",
            entityName: "SeoSettings",
            entityId: existing.Id.ToString());

        TempData["Info"] = "SEO ayarları güncellendi.";
        return RedirectToAction(nameof(Index));
    }

    // ---------------- Sayfa bazlı SEO override ----------------

    public async Task<IActionResult> Pages()
    {
        ViewBag.SiteActive = "seopages";
        var rows = await _context.PageSeos.AsNoTracking().ToDictionaryAsync(p => p.PageKey);
        // Katalogdaki her sayfa, varsa DB kaydıyla yoksa boş bir satırla eşleştirilir - migration henüz uygulanmadıysa bile sayfa çökmez.
        var list = SeoPageCatalog.All
            .Select(def => (Def: def, Row: rows.TryGetValue(def.Key, out var row) ? row : new PageSeo { PageKey = def.Key }))
            .ToList();
        return View(list);
    }

    [HttpGet]
    public async Task<IActionResult> PageEdit(string key)
    {
        var pageDef = SeoPageCatalog.Find(key);
        if (pageDef == null)
        {
            return NotFound();
        }

        ViewBag.SiteActive = "seopages";
        ViewBag.PageDef = pageDef;
        var pageSeo = await _context.PageSeos.FirstOrDefaultAsync(p => p.PageKey == key) ?? new PageSeo { PageKey = key };
        return View(pageSeo);
    }

    // GÜVENLİK: PageKey her zaman route parametresinden (key) gelir, postalanan model.PageKey hiç kullanılmaz - aksi halde form manipüle edilip başka bir sayfanın SEO kaydı üzerine yazılabilirdi.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PageEdit(string key, PageSeo model, IFormFile? ogImage, bool removeOgImage = false)
    {
        var pageDef = SeoPageCatalog.Find(key);
        if (pageDef == null)
        {
            return NotFound();
        }

        ViewBag.SiteActive = "seopages";
        ViewBag.PageDef = pageDef;

        var existing = await _context.PageSeos.FirstOrDefaultAsync(p => p.PageKey == key);

        if (!ModelState.IsValid)
        {
            model.PageKey = key;
            model.OgImageUrl = existing?.OgImageUrl;
            return View(model);
        }

        var oldOgImageUrl = existing?.OgImageUrl;

        if (ogImage is { Length: > 0 })
        {
            var uploadError = await ImageUploadValidator.ValidateAsync(ogImage);
            if (uploadError != null)
            {
                model.PageKey = key;
                model.OgImageUrl = existing?.OgImageUrl;
                ModelState.AddModelError(string.Empty, $"Open Graph görseli: {uploadError}");
                return View(model);
            }
        }

        if (existing == null)
        {
            existing = new PageSeo { PageKey = key };
            _context.PageSeos.Add(existing);
        }

        existing.Title = model.Title;
        existing.MetaDescription = model.MetaDescription;
        existing.CanonicalUrl = model.CanonicalUrl;
        existing.NoIndex = model.NoIndex;
        existing.NoFollow = model.NoFollow;
        existing.OgTitle = model.OgTitle;
        existing.OgDescription = model.OgDescription;

        if (ogImage is { Length: > 0 })
        {
            existing.OgImageUrl = await SaveImageAsync(ogImage, pageDef.Label);
        }
        else if (removeOgImage)
        {
            existing.OgImageUrl = null;
        }

        await _context.SaveChangesAsync();
        SeoCache.InvalidatePages(_cache);

        if (oldOgImageUrl != existing.OgImageUrl)
        {
            DeleteImageFileIfExists(oldOgImageUrl);
        }

        await _audit.LogAsync(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            User.Identity?.Name,
            "Sayfa SEO ayarları güncellendi",
            entityName: "PageSeo",
            entityId: existing.Id.ToString(),
            details: pageDef.Label);

        TempData["Info"] = $"\"{pageDef.Label}\" sayfasının SEO ayarları güncellendi.";
        return RedirectToAction(nameof(Pages));
    }

    private async Task<string> SaveImageAsync(IFormFile file, string nameHint)
    {
        var uploadsDir = Path.Combine(_env.WebRootPath, "uploads", "seo");
        Directory.CreateDirectory(uploadsDir);

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var fileName = SlugHelper.BuildFileName(nameHint, "seo-gorsel", ext);
        var fullPath = Path.Combine(uploadsDir, fileName);

        using (var stream = new FileStream(fullPath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        return $"/uploads/seo/{fileName}";
    }

    private void DeleteImageFileIfExists(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return;
        }
        try
        {
            var fullPath = Path.Combine(_env.WebRootPath, imageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (System.IO.File.Exists(fullPath))
            {
                System.IO.File.Delete(fullPath);
            }
        }
        catch (IOException)
        {
            // Dosya kilitli/erişilemez olabilir - veritabanı işlemi zaten tamamlandı, sessizce vazgeçilir.
        }
    }
}
