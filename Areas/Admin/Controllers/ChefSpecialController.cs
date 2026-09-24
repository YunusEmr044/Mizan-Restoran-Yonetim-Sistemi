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

// "Şefin Önerisi / Günün Yemeği": menüden bir ürün seçilir, başlangıç-bitiş tarihiyle otomatik yayınlanır/kalkar.
[Area("Admin")]
[Authorize(Policy = Permissions.SiteContent.Manage)]
public class ChefSpecialController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly Microsoft.AspNetCore.Hosting.IWebHostEnvironment _env;
    private readonly IMemoryCache _cache;
    private readonly AuditService _audit;

    public ChefSpecialController(ApplicationDbContext context, Microsoft.AspNetCore.Hosting.IWebHostEnvironment env, IMemoryCache cache, AuditService audit)
    {
        _context = context;
        _env = env;
        _cache = cache;
        _audit = audit;
    }

    public async Task<IActionResult> Index()
    {
        var specials = await _context.ChefSpecials
            .AsNoTracking()
            .Include(c => c.MenuItem)
            .OrderByDescending(c => c.StartDate)
            .ToListAsync();
        return View(specials);
    }

    public async Task<IActionResult> Create()
    {
        await PopulateMenuItemsAsync();
        return View(new ChefSpecial());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ChefSpecial model, IFormFile? imageFile)
    {
        if (model.EndDate < model.StartDate)
        {
            ModelState.AddModelError(nameof(model.EndDate), "Bitiş tarihi başlangıçtan önce olamaz.");
        }
        var uploadError = imageFile != null ? await ImageUploadValidator.ValidateAsync(imageFile) : null;
        if (uploadError != null)
        {
            ModelState.AddModelError(nameof(imageFile), uploadError);
        }
        if (!ModelState.IsValid || !await _context.MenuItems.AnyAsync(m => m.Id == model.MenuItemId))
        {
            if (!await _context.MenuItems.AnyAsync(m => m.Id == model.MenuItemId))
            {
                ModelState.AddModelError(nameof(model.MenuItemId), "Geçerli bir ürün seçin.");
            }
            await PopulateMenuItemsAsync();
            return View(model);
        }

        if (imageFile != null)
        {
            model.ImageUrlOverride = await SaveImageAsync(imageFile, model.TitleOverride);
        }

        _context.ChefSpecials.Add(model);
        await _context.SaveChangesAsync();
        HomePreviewCache.InvalidateChefSpecial(_cache);
        await _audit.LogAsync(User.FindFirstValue(ClaimTypes.NameIdentifier), User.Identity?.Name,
            "Şefin önerisi eklendi", entityName: "ChefSpecial", entityId: model.Id.ToString(),
            details: $"{model.StartDate:dd.MM.yyyy} - {model.EndDate:dd.MM.yyyy}");
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var special = await _context.ChefSpecials.FindAsync(id);
        if (special == null) return NotFound();
        await PopulateMenuItemsAsync();
        return View(special);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, ChefSpecial model, IFormFile? imageFile, bool removeImage = false)
    {
        if (id != model.Id)
        {
            return NotFound();
        }

        var existing = await _context.ChefSpecials.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        if (existing == null)
        {
            return NotFound();
        }

        if (model.EndDate < model.StartDate)
        {
            ModelState.AddModelError(nameof(model.EndDate), "Bitiş tarihi başlangıçtan önce olamaz.");
        }
        var uploadError = imageFile != null ? await ImageUploadValidator.ValidateAsync(imageFile) : null;
        if (uploadError != null)
        {
            ModelState.AddModelError(nameof(imageFile), uploadError);
        }
        if (!await _context.MenuItems.AnyAsync(m => m.Id == model.MenuItemId))
        {
            ModelState.AddModelError(nameof(model.MenuItemId), "Geçerli bir ürün seçin.");
        }
        if (!ModelState.IsValid)
        {
            await PopulateMenuItemsAsync();
            return View(model);
        }

        string? newImageUrl = null;
        if (imageFile != null)
        {
            newImageUrl = await SaveImageAsync(imageFile, model.TitleOverride);
            model.ImageUrlOverride = newImageUrl;
        }
        else if (removeImage)
        {
            model.ImageUrlOverride = null;
        }
        else
        {
            model.ImageUrlOverride = existing.ImageUrlOverride;
        }

        _context.Update(model);
        await _context.SaveChangesAsync();

        if (existing.ImageUrlOverride != model.ImageUrlOverride)
        {
            DeleteImageFileIfExists(existing.ImageUrlOverride);
        }

        HomePreviewCache.InvalidateChefSpecial(_cache);
        await _audit.LogAsync(User.FindFirstValue(ClaimTypes.NameIdentifier), User.Identity?.Name,
            "Şefin önerisi güncellendi", entityName: "ChefSpecial", entityId: id.ToString(),
            details: $"{model.StartDate:dd.MM.yyyy} - {model.EndDate:dd.MM.yyyy}");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var special = await _context.ChefSpecials.FindAsync(id);
        if (special != null)
        {
            _context.ChefSpecials.Remove(special);
            await _context.SaveChangesAsync();
            DeleteImageFileIfExists(special.ImageUrlOverride);
            HomePreviewCache.InvalidateChefSpecial(_cache);
            await _audit.LogAsync(User.FindFirstValue(ClaimTypes.NameIdentifier), User.Identity?.Name,
                "Şefin önerisi silindi", entityName: "ChefSpecial", entityId: id.ToString());
        }
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateMenuItemsAsync()
    {
        ViewBag.MenuItems = await _context.MenuItems
            .AsNoTracking()
            .Where(m => m.IsAvailable)
            .OrderBy(m => m.Name)
            .ToListAsync();
    }

    private async Task<string> SaveImageAsync(IFormFile file, string? nameHint)
    {
        var uploadsDir = Path.Combine(_env.WebRootPath, "uploads", "home");
        Directory.CreateDirectory(uploadsDir);
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var fileName = SlugHelper.BuildFileName(nameHint, "sefin-onerisi", ext);
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
        }
    }
}
