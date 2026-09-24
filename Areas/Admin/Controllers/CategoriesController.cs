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

[Area("Admin")]
[Authorize(Policy = Permissions.Products.Manage)]
public class CategoriesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly AuditService _audit;
    private readonly IMemoryCache _cache;

    public CategoriesController(ApplicationDbContext context, AuditService audit, IMemoryCache cache)
    {
        _context = context;
        _audit = audit;
        _cache = cache;
    }

    public async Task<IActionResult> Index()
    {
        return View(await _context.Categories
            .AsNoTracking()
            .Include(c => c.MenuItems)
            .OrderBy(c => c.DisplayOrder)
            .ToListAsync());
    }

    public IActionResult Create() => View(new Category());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Category category)
    {
        if (!ModelState.IsValid)
        {
            return View(category);
        }

        _context.Categories.Add(category);
        await _context.SaveChangesAsync();
        MenuCache.Invalidate(_cache);
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var category = await _context.Categories.FindAsync(id);
        if (category == null)
        {
            return NotFound();
        }
        return View(category);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Category category)
    {
        if (id != category.Id)
        {
            return NotFound();
        }
        if (!ModelState.IsValid)
        {
            return View(category);
        }

        _context.Update(category);
        await _context.SaveChangesAsync();
        MenuCache.Invalidate(_cache);
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int id)
    {
        var category = await _context.Categories.FindAsync(id);
        if (category == null)
        {
            return NotFound();
        }
        return View(category);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var category = await _context.Categories.FindAsync(id);
        if (category != null)
        {
            var categoryName = category.Name;
            _context.Categories.Remove(category);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // MenuItem -> Category ilişkisi Restrict - kategoriye bağlı ürün varsa veritabanı silmeyi reddeder.
                TempData["Error"] = "Bu kategoriye bağlı ürünler var, önce onları başka bir kategoriye taşıyın veya silin.";
                return RedirectToAction(nameof(Index));
            }

            MenuCache.Invalidate(_cache);

            await _audit.LogAsync(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                User.Identity?.Name,
                "Kategori silindi",
                entityName: "Category",
                entityId: id.ToString(),
                details: categoryName);
        }
        return RedirectToAction(nameof(Index));
    }
}
