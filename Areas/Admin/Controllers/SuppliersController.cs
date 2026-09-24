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
[Authorize(Policy = Permissions.Inventory.Manage)]
public class SuppliersController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly AuditService _audit;

    public SuppliersController(ApplicationDbContext context, AuditService audit)
    {
        _context = context;
        _audit = audit;
    }

    public async Task<IActionResult> Index()
    {
        var suppliers = await _context.Suppliers.AsNoTracking().OrderBy(s => s.Name).ToListAsync();
        return View(suppliers);
    }

    public IActionResult Create() => View(new Supplier());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Supplier supplier)
    {
        if (!ModelState.IsValid)
        {
            return View(supplier);
        }
        _context.Suppliers.Add(supplier);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var supplier = await _context.Suppliers.FindAsync(id);
        if (supplier == null)
        {
            return NotFound();
        }
        return View(supplier);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Supplier supplier)
    {
        if (id != supplier.Id)
        {
            return NotFound();
        }
        if (!ModelState.IsValid)
        {
            return View(supplier);
        }
        _context.Update(supplier);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int id)
    {
        var supplier = await _context.Suppliers.FindAsync(id);
        if (supplier == null)
        {
            return NotFound();
        }
        return View(supplier);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var supplier = await _context.Suppliers.FindAsync(id);
        if (supplier != null)
        {
            var supplierName = supplier.Name;
            _context.Suppliers.Remove(supplier);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                TempData["Error"] = "Bu tedarikçiye bağlı stok kalemi veya satın alma kaydı var, önce onları kaldırın.";
                return RedirectToAction(nameof(Index));
            }

            await _audit.LogAsync(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                User.Identity?.Name,
                "Tedarikçi silindi",
                entityName: "Supplier",
                entityId: id.ToString(),
                details: supplierName);
        }
        return RedirectToAction(nameof(Index));
    }
}
