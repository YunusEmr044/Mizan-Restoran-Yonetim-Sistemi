using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestoranYonetim.Data;
using RestoranYonetim.Models;
using RestoranYonetim.Security;
using RestoranYonetim.Services;

namespace RestoranYonetim.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = Permissions.Tables.Manage)]
public class AreasController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly AuditService _audit;

    public AreasController(ApplicationDbContext context, AuditService audit)
    {
        _context = context;
        _audit = audit;
    }

    public async Task<IActionResult> Index()
    {
        var areas = await _context.Areas
            .AsNoTracking()
            .Include(a => a.Branch)
            .ThenInclude(b => b!.Restaurant)
            .Include(a => a.Tables)
            .OrderBy(a => a.Name)
            .ToListAsync();
        return View(areas);
    }

    private async Task PopulateBranchesAsync()
    {
        ViewBag.Branches = await _context.Branches
            .AsNoTracking()
            .Include(b => b.Restaurant)
            .OrderBy(b => b.Name)
            .ToListAsync();
    }

    public async Task<IActionResult> Create()
    {
        await PopulateBranchesAsync();
        return View(new Area());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Area area)
    {
        if (!ModelState.IsValid)
        {
            await PopulateBranchesAsync();
            return View(area);
        }

        _context.Areas.Add(area);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var area = await _context.Areas.FindAsync(id);
        if (area == null)
        {
            return NotFound();
        }
        await PopulateBranchesAsync();
        return View(area);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Area area)
    {
        if (id != area.Id)
        {
            return NotFound();
        }
        if (!ModelState.IsValid)
        {
            await PopulateBranchesAsync();
            return View(area);
        }

        _context.Update(area);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int id)
    {
        var area = await _context.Areas
            .AsNoTracking()
            .Include(a => a.Branch)
            .ThenInclude(b => b!.Restaurant)
            .FirstOrDefaultAsync(a => a.Id == id);
        if (area == null)
        {
            return NotFound();
        }
        return View(area);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var area = await _context.Areas.FindAsync(id);
        if (area != null)
        {
            var areaName = area.Name;
            _context.Areas.Remove(area);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // RestaurantTable -> Area ilişkisi Restrict - alana bağlı masa varsa veritabanı silmeyi reddeder.
                TempData["Error"] = "Bu alana bağlı masalar var, önce onları silin veya başka bir alana taşıyın.";
                return RedirectToAction(nameof(Index));
            }

            await _audit.LogAsync(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                User.Identity?.Name,
                "Alan silindi",
                entityName: "Area",
                entityId: id.ToString(),
                details: areaName);
        }
        return RedirectToAction(nameof(Index));
    }
}
