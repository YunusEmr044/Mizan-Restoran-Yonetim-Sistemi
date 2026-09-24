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

[Area("Admin")]
[Authorize(Policy = Permissions.SiteContent.Manage)]
public class GalleryController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IWebHostEnvironment _env;
    private readonly IMemoryCache _cache;
    private readonly AuditService _audit;

    public GalleryController(ApplicationDbContext context, IWebHostEnvironment env, IMemoryCache cache, AuditService audit)
    {
        _context = context;
        _env = env;
        _cache = cache;
        _audit = audit;
    }

    public async Task<IActionResult> Index()
    {
        var images = await _context.GalleryImages.AsNoTracking().OrderBy(g => g.DisplayOrder).ToListAsync();
        return View(images);
    }

    public IActionResult Create() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(GalleryImage model, IFormFile? imageFile)
    {
        var uploadError = await ImageUploadValidator.ValidateAsync(imageFile);
        if (uploadError != null)
        {
            ModelState.AddModelError(string.Empty, uploadError);
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var uploadsDir = Path.Combine(_env.WebRootPath, "uploads", "gallery");
        Directory.CreateDirectory(uploadsDir);

        var ext = Path.GetExtension(imageFile!.FileName).ToLowerInvariant();
        var fileName = SlugHelper.BuildFileName(model.Caption, "galeri", ext);
        var fullPath = Path.Combine(uploadsDir, fileName);

        using (var stream = new FileStream(fullPath, FileMode.Create))
        {
            await imageFile.CopyToAsync(stream);
        }

        model.ImageUrl = $"/uploads/gallery/{fileName}";
        _context.GalleryImages.Add(model);
        await _context.SaveChangesAsync();
        HomePreviewCache.InvalidateGallery(_cache);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateOrder(int id, int displayOrder)
    {
        var image = await _context.GalleryImages.FindAsync(id);
        if (image == null)
        {
            return NotFound();
        }
        image.DisplayOrder = displayOrder;
        await _context.SaveChangesAsync();
        HomePreviewCache.InvalidateGallery(_cache);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleHomepage(int id)
    {
        var image = await _context.GalleryImages.FindAsync(id);
        if (image == null)
        {
            return NotFound();
        }
        image.ShowOnHomepage = !image.ShowOnHomepage;
        await _context.SaveChangesAsync();
        HomePreviewCache.InvalidateGallery(_cache);
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int id)
    {
        var image = await _context.GalleryImages.FindAsync(id);
        if (image == null)
        {
            return NotFound();
        }
        return View(image);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var image = await _context.GalleryImages.FindAsync(id);
        if (image != null)
        {
            var caption = image.Caption;
            _context.GalleryImages.Remove(image);
            await _context.SaveChangesAsync();
            HomePreviewCache.InvalidateGallery(_cache);

            var fullPath = Path.Combine(_env.WebRootPath, image.ImageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (System.IO.File.Exists(fullPath))
            {
                System.IO.File.Delete(fullPath);
            }

            await _audit.LogAsync(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                User.Identity?.Name,
                "Galeri görseli silindi",
                entityName: "GalleryImage",
                entityId: id.ToString(),
                details: caption);
        }
        return RedirectToAction(nameof(Index));
    }
}
