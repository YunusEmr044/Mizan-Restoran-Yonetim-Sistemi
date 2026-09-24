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

// Herkese açık restoran sitesinin içeriğini (ana sayfa, hakkımızda, iletişim, konum,
// rezervasyon formu açık/kapalı) yöneten tekil kayıt ekranı.
[Area("Admin")]
[Authorize(Policy = Permissions.SiteContent.Manage)]
public class SiteContentController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _env;
    private readonly IMemoryCache _cache;
    private readonly AuditService _audit;

    public SiteContentController(ApplicationDbContext context, IWebHostEnvironment env, IMemoryCache cache, AuditService audit)
    {
        _context = context;
        _env = env;
        _cache = cache;
        _audit = audit;
    }

    private async Task<SiteContent> GetOrCreateAsync()
    {
        var content = await _context.SiteContents.FirstOrDefaultAsync();
        if (content == null)
        {
            content = new SiteContent();
            _context.SiteContents.Add(content);
            await _context.SaveChangesAsync();
        }
        return content;
    }

    public async Task<IActionResult> Index()
    {
        var content = await GetOrCreateAsync();
        return View(content);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(SiteContent model, IFormFile? heroImage, IFormFile? aboutImage, IFormFile? logoImage, IFormFile? reservationCtaImage, IFormFile? qrPhoneImage,
        bool removeHeroImage = false, bool removeAboutImage = false, bool removeLogoImage = false, bool removeReservationCtaImage = false, bool removeQrPhoneImage = false)
    {
        var existing = await GetOrCreateAsync();
        if (!ModelState.IsValid)
        {
            model.Id = existing.Id;
            return View(model);
        }

        model.Id = existing.Id;
        model.HeroImageUrl = existing.HeroImageUrl;
        model.AboutImageUrl = existing.AboutImageUrl;
        model.LogoImageUrl = existing.LogoImageUrl;
        model.ReservationCtaImageUrl = existing.ReservationCtaImageUrl;
        model.QrPhoneImageUrl = existing.QrPhoneImageUrl;

        var oldHeroImageUrl = existing.HeroImageUrl;
        var oldAboutImageUrl = existing.AboutImageUrl;
        var oldLogoImageUrl = existing.LogoImageUrl;
        var oldReservationCtaImageUrl = existing.ReservationCtaImageUrl;
        var oldQrPhoneImageUrl = existing.QrPhoneImageUrl;

        // ÖNEMLİ: tema alanları bu formda yok, bound olmadıkları için model'de varsayılan gelir - existing'teki değerler korunmazsa SetValues temayı sessizce sıfırlardı.
        model.ThemePrimaryColor = existing.ThemePrimaryColor;
        model.ThemeDarkColor = existing.ThemeDarkColor;
        model.ThemeBackgroundColor = existing.ThemeBackgroundColor;
        model.ThemeTextColor = existing.ThemeTextColor;
        model.ThemeHeadingFont = existing.ThemeHeadingFont;
        model.ThemeBodyFont = existing.ThemeBodyFont;
        model.ThemeCornerStyle = existing.ThemeCornerStyle;
        model.AdminAccentColor = existing.AdminAccentColor;
        model.AdminSidebarColor = existing.AdminSidebarColor;
        model.AdminBackgroundColor = existing.AdminBackgroundColor;
        model.PublicBaseUrl = existing.PublicBaseUrl;

        foreach (var (file, label) in new[] { (heroImage, "Kapak görseli"), (aboutImage, "Hakkımızda görseli"), (logoImage, "Logo"), (reservationCtaImage, "Rezervasyon çağrısı görseli"), (qrPhoneImage, "QR bölümü telefon görseli") })
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

        if (heroImage is { Length: > 0 })
        {
            model.HeroImageUrl = await SaveImageAsync(heroImage, "kapak");
        }
        else if (removeHeroImage)
        {
            model.HeroImageUrl = null;
        }
        if (aboutImage is { Length: > 0 })
        {
            model.AboutImageUrl = await SaveImageAsync(aboutImage, "hakkimizda");
        }
        else if (removeAboutImage)
        {
            model.AboutImageUrl = null;
        }
        if (logoImage is { Length: > 0 })
        {
            model.LogoImageUrl = await SaveImageAsync(logoImage, "logo");
        }
        else if (removeLogoImage)
        {
            model.LogoImageUrl = null;
        }
        if (reservationCtaImage is { Length: > 0 })
        {
            model.ReservationCtaImageUrl = await SaveImageAsync(reservationCtaImage, "rezervasyon-cagrisi");
        }
        else if (removeReservationCtaImage)
        {
            model.ReservationCtaImageUrl = null;
        }
        if (qrPhoneImage is { Length: > 0 })
        {
            model.QrPhoneImageUrl = await SaveImageAsync(qrPhoneImage, "dijital-menu-mockup");
        }
        else if (removeQrPhoneImage)
        {
            model.QrPhoneImageUrl = null;
        }

        _context.Entry(existing).CurrentValues.SetValues(model);
        await _context.SaveChangesAsync();
        SiteContentCache.Invalidate(_cache);

        if (oldHeroImageUrl != model.HeroImageUrl) DeleteImageFileIfExists(oldHeroImageUrl);
        if (oldAboutImageUrl != model.AboutImageUrl) DeleteImageFileIfExists(oldAboutImageUrl);
        if (oldLogoImageUrl != model.LogoImageUrl) DeleteImageFileIfExists(oldLogoImageUrl);
        if (oldReservationCtaImageUrl != model.ReservationCtaImageUrl) DeleteImageFileIfExists(oldReservationCtaImageUrl);
        if (oldQrPhoneImageUrl != model.QrPhoneImageUrl) DeleteImageFileIfExists(oldQrPhoneImageUrl);

        await _audit.LogAsync(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            User.Identity?.Name,
            "Site içeriği güncellendi",
            entityName: "SiteContent",
            entityId: existing.Id.ToString());

        TempData["Info"] = "Site içeriği güncellendi.";
        return RedirectToAction(nameof(Index));
    }

    private async Task<string> SaveImageAsync(IFormFile file, string nameHint)
    {
        var uploadsDir = Path.Combine(_env.WebRootPath, "uploads", "site");
        Directory.CreateDirectory(uploadsDir);

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var fileName = SlugHelper.BuildFileName(nameHint, "gorsel", ext);
        var fullPath = Path.Combine(uploadsDir, fileName);

        using (var stream = new FileStream(fullPath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        return $"/uploads/site/{fileName}";
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
