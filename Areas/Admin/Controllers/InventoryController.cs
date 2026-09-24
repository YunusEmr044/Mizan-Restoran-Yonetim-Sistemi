using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RestoranYonetim.Data;
using RestoranYonetim.Models;
using RestoranYonetim.Security;
using RestoranYonetim.Services;

namespace RestoranYonetim.Areas.Admin.Controllers;

// Stok kalemleri: liste, CRUD, manuel düzeltme ve hareket geçmişi. Gerçek stok girişi/çıkışı satın alma/satış üzerinden otomatik olur (InventoryService); "Düzelt" sadece sayım farkı gibi istisnai durumlar içindir.
[Area("Admin")]
[Authorize(Policy = Permissions.Inventory.View)]
public class InventoryController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly InventoryService _inventoryService;
    private readonly AuditService _audit;

    public InventoryController(ApplicationDbContext context, InventoryService inventoryService, AuditService audit)
    {
        _context = context;
        _inventoryService = inventoryService;
        _audit = audit;
    }

    public async Task<IActionResult> Index()
    {
        var items = await _context.InventoryItems
            .AsNoTracking()
            .Include(i => i.Supplier)
            .OrderBy(i => i.Name)
            .ToListAsync();
        return View(items);
    }

    [Authorize(Policy = Permissions.Inventory.Manage)]
    public async Task<IActionResult> Create()
    {
        await PopulateSuppliersAsync();
        return View(new InventoryItem());
    }

    [HttpPost]
    [Authorize(Policy = Permissions.Inventory.Manage)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(InventoryItem item)
    {
        if (!ModelState.IsValid)
        {
            await PopulateSuppliersAsync(item.SupplierId);
            return View(item);
        }

        // Açılış stoğu da hareket geçmişine loglanır, aksi halde InventoryTransaction toplamı CurrentQuantity ile hiç örtüşmezdi.
        var openingQuantity = item.CurrentQuantity;
        item.CurrentQuantity = 0;
        _context.InventoryItems.Add(item);
        await _context.SaveChangesAsync();

        if (openingQuantity != 0)
        {
            await _inventoryService.AdjustAsync(item.Id, openingQuantity, "Yeni malzeme - açılış stoğu");
        }

        // InventoryTransaction kimin yaptığını tutmuyor (UserId/UserName alanı yok) - yeni kalem eklemesi burada audit'e ayrıca loglanır.
        await _audit.LogAsync(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            User.Identity?.Name,
            "Yeni stok kalemi eklendi",
            entityName: "InventoryItem",
            entityId: item.Id.ToString(),
            details: $"{item.Name} (açılış stoğu: {openingQuantity} {item.Unit})");

        return RedirectToAction(nameof(Index));
    }

    [Authorize(Policy = Permissions.Inventory.Manage)]
    public async Task<IActionResult> Edit(int id)
    {
        var item = await _context.InventoryItems.FindAsync(id);
        if (item == null)
        {
            return NotFound();
        }
        await PopulateSuppliersAsync(item.SupplierId);
        return View(item);
    }

    [HttpPost]
    [Authorize(Policy = Permissions.Inventory.Manage)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, InventoryItem item)
    {
        if (id != item.Id)
        {
            return NotFound();
        }
        if (!ModelState.IsValid)
        {
            await PopulateSuppliersAsync(item.SupplierId);
            return View(item);
        }

        // ÖNEMLİ: _context.Update(item) ile TÜM alanlar (CurrentQuantity dahil) POST'tan yazılırsa, InventoryTransaction kaydı bırakmadan stok miktarı değiştirilebilir (overposting) - bu yüzden sadece "profil" alanları burada güncellenir, miktar SADECE Duzelt action'ından değişir.
        var existing = await _context.InventoryItems.FindAsync(id);
        if (existing == null)
        {
            return NotFound();
        }

        existing.Name = item.Name;
        existing.Unit = item.Unit;
        existing.MinLevel = item.MinLevel;
        existing.UnitCost = item.UnitCost;
        existing.SupplierId = item.SupplierId;

        await _context.SaveChangesAsync();

        await _audit.LogAsync(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            User.Identity?.Name,
            "Stok kalemi bilgileri güncellendi",
            entityName: "InventoryItem",
            entityId: id.ToString(),
            details: $"{existing.Name} (min seviye: {existing.MinLevel} {existing.Unit}, birim maliyet: {existing.UnitCost:0.00} TL)");

        return RedirectToAction(nameof(Index));
    }

    [Authorize(Policy = Permissions.Inventory.Manage)]
    public async Task<IActionResult> Delete(int id)
    {
        var item = await _context.InventoryItems.AsNoTracking().Include(i => i.Supplier).FirstOrDefaultAsync(i => i.Id == id);
        if (item == null)
        {
            return NotFound();
        }
        return View(item);
    }

    [HttpPost, ActionName("Delete")]
    [Authorize(Policy = Permissions.Inventory.Manage)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var item = await _context.InventoryItems.FindAsync(id);
        if (item != null)
        {
            var itemName = item.Name;
            _context.InventoryItems.Remove(item);
            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                User.Identity?.Name,
                "Stok kalemi silindi",
                entityName: "InventoryItem",
                entityId: id.ToString(),
                details: itemName);
        }
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Hareketler(int id)
    {
        var item = await _context.InventoryItems.FindAsync(id);
        if (item == null)
        {
            return NotFound();
        }
        ViewBag.Item = item;
        var transactions = await _context.InventoryTransactions
            .AsNoTracking()
            .Where(t => t.InventoryItemId == id)
            .OrderByDescending(t => t.CreatedAt)
            .Take(200)
            .ToListAsync();
        return View(transactions);
    }

    [HttpPost]
    [Authorize(Policy = Permissions.Inventory.Manage)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Duzelt(int id, decimal newQuantity, string? note)
    {
        // Önceki miktar audit kaydında "eski -> yeni" göstermek için loglamadan önce ayrıca okunuyor.
        var before = await _context.InventoryItems.AsNoTracking()
            .Where(i => i.Id == id)
            .Select(i => (decimal?)i.CurrentQuantity)
            .FirstOrDefaultAsync();

        await _inventoryService.AdjustAsync(id, newQuantity, note);

        // InventoryTransaction bu düzeltmeyi kimin yaptığını tutmuyor - sayım farkı gibi hassas bir işlem burada audit'e loglanır.
        await _audit.LogAsync(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            User.Identity?.Name,
            "Stok manuel düzeltildi",
            entityName: "InventoryItem",
            entityId: id.ToString(),
            details: $"{before?.ToString("0.###") ?? "?"} -> {newQuantity:0.###}{(string.IsNullOrWhiteSpace(note) ? "" : $" ({note})")}");

        return RedirectToAction(nameof(Hareketler), new { id });
    }

    private async Task PopulateSuppliersAsync(int? selectedId = null)
    {
        var suppliers = await _context.Suppliers.AsNoTracking().OrderBy(s => s.Name).ToListAsync();
        ViewBag.Suppliers = new SelectList(suppliers, "Id", "Name", selectedId);
    }
}
