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
    private readonly ImageStorageService _imageStorage;
    private readonly IMemoryCache _cache;
    private readonly AuditService _audit;

    public GalleryController(ApplicationDbContext context, IMemoryCache cache, AuditService audit, ImageStorageService imageStorage)
    {
        _context = context;
        _imageStorage = imageStorage;
        _cache = cache;
        _audit = audit;
    }

    public async Task<IActionResult> Index()
    {
        var images = await _context.GalleryImages.AsNoTracking().OrderBy(g => g.DisplayOrder).ToListAsync();
        return View(images);
    }

    public IActionResult Create() => View(new GalleryImage());

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

        model.ImageUrl = await _imageStorage.SaveAsync(imageFile!, "gallery", model.Caption, "galeri");
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

            _imageStorage.Delete(image.ImageUrl);

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
