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

// Satın alma kayıtları: bir tedarikçiden alınan malzemeler tek seferde girilir, kaydedildiğinde ilgili stok kalemlerinin miktarı otomatik artar (InventoryService).
[Area("Admin")]
[Authorize(Policy = Permissions.Inventory.Manage)]
public class PurchasesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly InventoryService _inventoryService;
    private readonly AuditService _audit;

    public PurchasesController(ApplicationDbContext context, InventoryService inventoryService, AuditService audit)
    {
        _context = context;
        _inventoryService = inventoryService;
        _audit = audit;
    }

    public async Task<IActionResult> Index()
    {
        var purchases = await _context.Purchases
            .AsNoTracking()
            .Include(p => p.Supplier)
            .Include(p => p.Items)
            .OrderByDescending(p => p.PurchaseDate)
            .Take(200)
            .ToListAsync();
        return View(purchases);
    }

    public async Task<IActionResult> Create()
    {
        await PopulateDropdownsAsync();
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(int supplierId, string? invoiceNumber, DateTime? purchaseDate,
        List<int> inventoryItemIds, List<decimal> quantities, List<decimal> unitPrices)
    {
        var supplier = await _context.Suppliers.FindAsync(supplierId);
        if (supplier == null)
        {
            TempData["Error"] = "Lütfen bir tedarikçi seçin.";
            await PopulateDropdownsAsync();
            return View();
        }

        var purchase = new Purchase
        {
            SupplierId = supplierId,
            InvoiceNumber = invoiceNumber,
            PurchaseDate = purchaseDate ?? DateTime.Now
        };

        for (var i = 0; i < inventoryItemIds.Count; i++)
        {
            if (i >= quantities.Count || i >= unitPrices.Count)
            {
                break;
            }
            if (quantities[i] <= 0)
            {
                continue;
            }

            var inventoryItem = await _context.InventoryItems.FindAsync(inventoryItemIds[i]);
            if (inventoryItem == null)
            {
                continue;
            }

            purchase.Items.Add(new PurchaseItem
            {
                InventoryItem = inventoryItem,
                InventoryItemId = inventoryItem.Id,
                Quantity = quantities[i],
                UnitPrice = unitPrices[i]
            });
        }

        if (!purchase.Items.Any())
        {
            TempData["Error"] = "En az bir malzeme ve miktar girmelisiniz.";
            await PopulateDropdownsAsync();
            return View();
        }

        _context.Purchases.Add(purchase);
        _inventoryService.ReceivePurchase(purchase);
        await _context.SaveChangesAsync();

        // Purchase/PurchaseItem tablolarında bu kaydı kimin girdiğine dair alan yok - finansal kayıt olduğu için burada audit'e loglanır.
        var total = purchase.Items.Sum(i => i.Quantity * i.UnitPrice);
        await _audit.LogAsync(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            User.Identity?.Name,
            "Satın alma kaydedildi",
            entityName: "Purchase",
            entityId: purchase.Id.ToString(),
            details: $"{supplier.Name}, {purchase.Items.Count} kalem, toplam {total:0.00} TL");

        TempData["Info"] = "Satın alma kaydedildi, stoklar güncellendi.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Detay(int id)
    {
        var purchase = await _context.Purchases
            .AsNoTracking()
            .Include(p => p.Supplier)
            .Include(p => p.Items).ThenInclude(i => i.InventoryItem)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (purchase == null)
        {
            return NotFound();
        }
        return View(purchase);
    }

    private async Task PopulateDropdownsAsync()
    {
        ViewBag.Suppliers = new SelectList(await _context.Suppliers.AsNoTracking().OrderBy(s => s.Name).ToListAsync(), "Id", "Name");
        ViewBag.InventoryItems = await _context.InventoryItems.AsNoTracking().OrderBy(i => i.Name).ToListAsync();
    }
}
