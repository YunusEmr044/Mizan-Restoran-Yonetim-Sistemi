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
public class BranchesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly AuditService _audit;

    public BranchesController(ApplicationDbContext context, AuditService audit)
    {
        _context = context;
        _audit = audit;
    }

    public async Task<IActionResult> Index()
    {
        var branches = await _context.Branches
            .AsNoTracking()
            .Include(b => b.Restaurant)
            .Include(b => b.Areas)
            .ThenInclude(a => a.Tables)
            .OrderBy(b => b.Name)
            .ToListAsync();

        var branchIds = branches.Select(b => b.Id).ToList();
        var revenueByBranch = await _context.Adisyonlar
            .AsNoTracking()
            .Where(a => a.Status == AdisyonStatus.Odendi && a.ClosedAt != null && branchIds.Contains(a.Table!.Area!.BranchId))
            .GroupBy(a => a.Table!.Area!.BranchId)
            .Select(g => new { BranchId = g.Key, Revenue = g.Sum(a => a.Orders.Where(o => !o.IsDraft && o.Status != OrderStatus.IptalEdildi).SelectMany(o => o.Items.Where(i => i.Status != OrderStatus.IptalEdildi)).Sum(i => i.Quantity * i.UnitPrice)) })
            .ToDictionaryAsync(x => x.BranchId, x => x.Revenue);

        ViewBag.RevenueByBranch = revenueByBranch;
        return View(branches);
    }

    private async Task PopulateRestaurantsAsync()
    {
        ViewBag.Restaurants = await _context.Restaurants.AsNoTracking().OrderBy(r => r.Name).ToListAsync();
    }

    public async Task<IActionResult> Create()
    {
        await PopulateRestaurantsAsync();
        return View(new Branch());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Branch branch)
    {
        if (!ModelState.IsValid)
        {
            await PopulateRestaurantsAsync();
            return View(branch);
        }

        _context.Branches.Add(branch);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var branch = await _context.Branches.FindAsync(id);
        if (branch == null)
        {
            return NotFound();
        }
        await PopulateRestaurantsAsync();
        return View(branch);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Branch branch)
    {
        if (id != branch.Id)
        {
            return NotFound();
        }
        if (!ModelState.IsValid)
        {
            await PopulateRestaurantsAsync();
            return View(branch);
        }

        _context.Update(branch);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int id)
    {
        var branch = await _context.Branches
            .AsNoTracking()
            .Include(b => b.Restaurant)
            .FirstOrDefaultAsync(b => b.Id == id);
        if (branch == null)
        {
            return NotFound();
        }
        return View(branch);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var branch = await _context.Branches.FindAsync(id);
        if (branch != null)
        {
            var branchName = branch.Name;
            _context.Branches.Remove(branch);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Area -> Branch ilişkisi Restrict - şubeye bağlı alan varsa veritabanı silmeyi reddeder.
                TempData["Error"] = "Bu şubeye bağlı alanlar var, önce onları silin veya başka bir şubeye taşıyın.";
                return RedirectToAction(nameof(Index));
            }

            await _audit.LogAsync(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                User.Identity?.Name,
                "Şube silindi",
                entityName: "Branch",
                entityId: id.ToString(),
                details: branchName);
        }
        return RedirectToAction(nameof(Index));
    }
}
