using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RestoranYonetim.Data;
using RestoranYonetim.Models;
using RestoranYonetim.Security;
using RestoranYonetim.Services;

namespace RestoranYonetim.Areas.Admin.Controllers;

// "Ana Sayfa Düzeni" admin ekranı: Hero slaytları, istatistik şeridi, özellikler, bölüm sıralaması ve çalışma saatleri buradan yönetilir.
[Area("Admin")]
[Authorize(Policy = Permissions.SiteContent.Manage)]
public class HomePageController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly Microsoft.AspNetCore.Hosting.IWebHostEnvironment _env;
    private readonly IMemoryCache _cache;
    private readonly AuditService _audit;

    public HomePageController(ApplicationDbContext context, Microsoft.AspNetCore.Hosting.IWebHostEnvironment env, IMemoryCache cache, AuditService audit)
    {
        _context = context;
        _env = env;
        _cache = cache;
        _audit = audit;
    }

    public IActionResult Index() => RedirectToAction(nameof(Sections));

    // ---------------- Bölüm sırası / aç-kapa ----------------

    public async Task<IActionResult> Sections()
    {
        var rows = await _context.HomeSections.AsNoTracking().OrderBy(s => s.DisplayOrder).ToListAsync();
        // Katalogda olup DB'de henüz olmayan satırlar da gösterilsin diye katalogla birleştiriliyor.
        var merged = HomeSectionCatalog.All
            .Select(def => new
            {
                Definition = def,
                Row = rows.FirstOrDefault(r => r.Key == def.Key)
            })
            .OrderBy(x => x.Row?.DisplayOrder ?? x.Definition.DefaultOrder)
            .ToList();

        ViewBag.Merged = merged;
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleSection(string key)
    {
        var section = await _context.HomeSections.FirstOrDefaultAsync(s => s.Key == key);
        if (section == null)
        {
            return NotFound();
        }
        section.IsEnabled = !section.IsEnabled;
        await _context.SaveChangesAsync();
        HomePreviewCache.InvalidateSections(_cache);
        await _audit.LogAsync(User.FindFirstValue(ClaimTypes.NameIdentifier), User.Identity?.Name,
            section.IsEnabled ? "Ana sayfa bölümü açıldı" : "Ana sayfa bölümü kapatıldı",
            entityName: "HomeSection", entityId: key);
        return RedirectToAction(nameof(Sections));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReorderSections([FromBody] List<string> orderedKeys)
    {
        if (orderedKeys == null || orderedKeys.Count == 0)
        {
            return BadRequest();
        }

        var rows = await _context.HomeSections.ToListAsync();
        for (var i = 0; i < orderedKeys.Count; i++)
        {
            var row = rows.FirstOrDefault(r => r.Key == orderedKeys[i]);
            if (row != null)
            {
                row.DisplayOrder = i;
            }
        }
        await _context.SaveChangesAsync();
        HomePreviewCache.InvalidateSections(_cache);
        await _audit.LogAsync(User.FindFirstValue(ClaimTypes.NameIdentifier), User.Identity?.Name,
            "Ana sayfa bölüm sırası değiştirildi", entityName: "HomeSection");
        return Ok();
    }

    // ---------------- Hero slaytları ----------------

    public async Task<IActionResult> Hero()
    {
        var slides = await _context.HeroSlides.AsNoTracking().OrderBy(h => h.DisplayOrder).ToListAsync();
        return View(slides);
    }

    public IActionResult HeroCreate() => View(new HeroSlide());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> HeroCreate(HeroSlide model, IFormFile? imageFile, IFormFile? mobileImageFile)
    {
        await ValidateOptionalImageAsync(imageFile, nameof(imageFile));
        await ValidateOptionalImageAsync(mobileImageFile, nameof(mobileImageFile));
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        model.ImageUrl = await SaveImageIfProvidedAsync(imageFile, model.Title) ?? model.ImageUrl;
        model.MobileImageUrl = await SaveImageIfProvidedAsync(mobileImageFile, model.Title) ?? model.MobileImageUrl;

        _context.HeroSlides.Add(model);
        await _context.SaveChangesAsync();
        HomePreviewCache.InvalidateHero(_cache);
        await _audit.LogAsync(User.FindFirstValue(ClaimTypes.NameIdentifier), User.Identity?.Name,
            "Hero slaytı eklendi", entityName: "HeroSlide", entityId: model.Id.ToString(), details: model.Title);
        return RedirectToAction(nameof(Hero));
    }

    public async Task<IActionResult> HeroEdit(int id)
    {
        var slide = await _context.HeroSlides.FindAsync(id);
        if (slide == null)
        {
            return NotFound();
        }
        return View(slide);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> HeroEdit(int id, HeroSlide model, IFormFile? imageFile, IFormFile? mobileImageFile, bool removeImage = false, bool removeMobileImage = false)
    {
        if (id != model.Id)
        {
            return NotFound();
        }

        var existing = await _context.HeroSlides.AsNoTracking().FirstOrDefaultAsync(h => h.Id == id);
        if (existing == null)
        {
            return NotFound();
        }

        await ValidateOptionalImageAsync(imageFile, nameof(imageFile));
        await ValidateOptionalImageAsync(mobileImageFile, nameof(mobileImageFile));
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var newImageUrl = await SaveImageIfProvidedAsync(imageFile, model.Title);
        var newMobileImageUrl = await SaveImageIfProvidedAsync(mobileImageFile, model.Title);
        model.ImageUrl = newImageUrl ?? (removeImage ? null : existing.ImageUrl);
        model.MobileImageUrl = newMobileImageUrl ?? (removeMobileImage ? null : existing.MobileImageUrl);

        _context.Update(model);
        await _context.SaveChangesAsync();

        if (existing.ImageUrl != model.ImageUrl) DeleteImageFileIfExists(existing.ImageUrl);
        if (existing.MobileImageUrl != model.MobileImageUrl) DeleteImageFileIfExists(existing.MobileImageUrl);

        HomePreviewCache.InvalidateHero(_cache);
        await _audit.LogAsync(User.FindFirstValue(ClaimTypes.NameIdentifier), User.Identity?.Name,
            "Hero slaytı güncellendi", entityName: "HeroSlide", entityId: id.ToString(), details: model.Title);
        return RedirectToAction(nameof(Hero));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> HeroDelete(int id)
    {
        var slide = await _context.HeroSlides.FindAsync(id);
        if (slide != null)
        {
            _context.HeroSlides.Remove(slide);
            await _context.SaveChangesAsync();
            DeleteImageFileIfExists(slide.ImageUrl);
            DeleteImageFileIfExists(slide.MobileImageUrl);
            HomePreviewCache.InvalidateHero(_cache);
            await _audit.LogAsync(User.FindFirstValue(ClaimTypes.NameIdentifier), User.Identity?.Name,
                "Hero slaytı silindi", entityName: "HeroSlide", entityId: id.ToString(), details: slide.Title);
        }
        return RedirectToAction(nameof(Hero));
    }

    // ---------------- İstatistikler ----------------

    public async Task<IActionResult> Stats()
    {
        var stats = await _context.HomeStats.AsNoTracking().OrderBy(s => s.DisplayOrder).ToListAsync();
        return View(stats);
    }

    public IActionResult StatsCreate() => View(new HomeStat());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StatsCreate(HomeStat model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }
        _context.HomeStats.Add(model);
        await _context.SaveChangesAsync();
        HomePreviewCache.InvalidateStats(_cache);
        await _audit.LogAsync(User.FindFirstValue(ClaimTypes.NameIdentifier), User.Identity?.Name,
            "İstatistik eklendi", entityName: "HomeStat", entityId: model.Id.ToString(), details: $"{model.Value} {model.Label}");
        return RedirectToAction(nameof(Stats));
    }

    public async Task<IActionResult> StatsEdit(int id)
    {
        var stat = await _context.HomeStats.FindAsync(id);
        if (stat == null) return NotFound();
        return View(stat);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StatsEdit(int id, HomeStat model)
    {
        if (id != model.Id || !ModelState.IsValid)
        {
            return View(model);
        }
        _context.Update(model);
        await _context.SaveChangesAsync();
        HomePreviewCache.InvalidateStats(_cache);
        await _audit.LogAsync(User.FindFirstValue(ClaimTypes.NameIdentifier), User.Identity?.Name,
            "İstatistik güncellendi", entityName: "HomeStat", entityId: id.ToString(), details: $"{model.Value} {model.Label}");
        return RedirectToAction(nameof(Stats));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StatsDelete(int id)
    {
        var stat = await _context.HomeStats.FindAsync(id);
        if (stat != null)
        {
            _context.HomeStats.Remove(stat);
            await _context.SaveChangesAsync();
            HomePreviewCache.InvalidateStats(_cache);
            await _audit.LogAsync(User.FindFirstValue(ClaimTypes.NameIdentifier), User.Identity?.Name,
                "İstatistik silindi", entityName: "HomeStat", entityId: id.ToString(), details: $"{stat.Value} {stat.Label}");
        }
        return RedirectToAction(nameof(Stats));
    }

    // ---------------- Öne çıkan özellikler ----------------

    public async Task<IActionResult> Features()
    {
        var features = await _context.HomeFeatures.AsNoTracking().OrderBy(f => f.DisplayOrder).ToListAsync();
        return View(features);
    }

    public IActionResult FeaturesCreate() => View(new HomeFeature());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> FeaturesCreate(HomeFeature model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }
        _context.HomeFeatures.Add(model);
        await _context.SaveChangesAsync();
        HomePreviewCache.InvalidateFeatures(_cache);
        await _audit.LogAsync(User.FindFirstValue(ClaimTypes.NameIdentifier), User.Identity?.Name,
            "Özellik eklendi", entityName: "HomeFeature", entityId: model.Id.ToString(), details: model.Title);
        return RedirectToAction(nameof(Features));
    }

    public async Task<IActionResult> FeaturesEdit(int id)
    {
        var feature = await _context.HomeFeatures.FindAsync(id);
        if (feature == null) return NotFound();
        return View(feature);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> FeaturesEdit(int id, HomeFeature model)
    {
        if (id != model.Id || !ModelState.IsValid)
        {
            return View(model);
        }
        _context.Update(model);
        await _context.SaveChangesAsync();
        HomePreviewCache.InvalidateFeatures(_cache);
        await _audit.LogAsync(User.FindFirstValue(ClaimTypes.NameIdentifier), User.Identity?.Name,
            "Özellik güncellendi", entityName: "HomeFeature", entityId: id.ToString(), details: model.Title);
        return RedirectToAction(nameof(Features));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> FeaturesDelete(int id)
    {
        var feature = await _context.HomeFeatures.FindAsync(id);
        if (feature != null)
        {
            _context.HomeFeatures.Remove(feature);
            await _context.SaveChangesAsync();
            HomePreviewCache.InvalidateFeatures(_cache);
            await _audit.LogAsync(User.FindFirstValue(ClaimTypes.NameIdentifier), User.Identity?.Name,
                "Özellik silindi", entityName: "HomeFeature", entityId: id.ToString(), details: feature.Title);
        }
        return RedirectToAction(nameof(Features));
    }

    // ---------------- Çalışma saatleri ----------------

    public async Task<IActionResult> WorkingHours()
    {
        var days = await _context.WorkingHoursDays.AsNoTracking().OrderBy(d => d.DayOfWeek).ToListAsync();
        return View(days);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> WorkingHours(List<WorkingHoursDay> model)
    {
        var existing = await _context.WorkingHoursDays.ToListAsync();
        foreach (var posted in model)
        {
            var row = existing.FirstOrDefault(d => d.DayOfWeek == posted.DayOfWeek);
            if (row == null) continue;
            row.IsClosed = posted.IsClosed;
            row.OpenTime = posted.OpenTime;
            row.CloseTime = posted.CloseTime;
        }
        await _context.SaveChangesAsync();
        HomePreviewCache.InvalidateWorkingHours(_cache);
        await _audit.LogAsync(User.FindFirstValue(ClaimTypes.NameIdentifier), User.Identity?.Name,
            "Çalışma saatleri güncellendi", entityName: "WorkingHoursDay");
        TempData["Info"] = "Çalışma saatleri kaydedildi.";
        return RedirectToAction(nameof(WorkingHours));
    }

    // ---------------- Yardımcılar ----------------

    private async Task ValidateOptionalImageAsync(IFormFile? file, string fieldName)
    {
        if (file == null) return;
        var error = await ImageUploadValidator.ValidateAsync(file);
        if (error != null)
        {
            ModelState.AddModelError(fieldName, error);
        }
    }

    private async Task<string?> SaveImageIfProvidedAsync(IFormFile? file, string? nameHint = null)
    {
        if (file == null || file.Length == 0)
        {
            return null;
        }

        var uploadsDir = Path.Combine(_env.WebRootPath, "uploads", "home");
        Directory.CreateDirectory(uploadsDir);

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var fileName = SlugHelper.BuildFileName(nameHint, "ana-sayfa", ext);
        var fullPath = Path.Combine(uploadsDir, fileName);

        using (var stream = new FileStream(fullPath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        return $"/uploads/home/{fileName}";
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
            // Dosya kilitli/erişilemezse sessizce geç - DB kaydı zaten güncellendi, bu ikincil bir temizlik adımı.
        }
    }
}
