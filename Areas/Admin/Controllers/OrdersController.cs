using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestoranYonetim.Data;
using RestoranYonetim.Extensions;
using RestoranYonetim.Models;
using RestoranYonetim.Security;
using RestoranYonetim.Services;

namespace RestoranYonetim.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = Permissions.Reports.View)]
public class OrdersController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly AuditService _audit;
    private readonly InventoryService _inventoryService;

    public OrdersController(ApplicationDbContext context, AuditService audit, InventoryService inventoryService)
    {
        _context = context;
        _audit = audit;
        _inventoryService = inventoryService;
    }

    public async Task<IActionResult> Index()
    {
        var orders = await _context.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .Include(o => o.Table)
            .Include(o => o.Adisyon)
            .OrderByDescending(o => o.CreatedAt)
            .Take(200)
            .ToListAsync();
        return View(orders);
    }

    // Takılı kalmış siparişleri yönetim elle iptal edebilsin diye. Kalemleri iptal eder (silmez) - geçmişte kalır, raporlarda IptalEdildi görünür.
    [HttpPost]
    [Authorize(Policy = Permissions.Orders.Cancel)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelOrder(int id)
    {
        var order = await _context.Orders.Include(o => o.Items).Include(o => o.Table).Include(o => o.Adisyon).ThenInclude(a => a!.Payments)
            .FirstOrDefaultAsync(o => o.Id == id);
        if (order == null)
        {
            return NotFound();
        }

        if (order.Adisyon?.IsClosed == true)
        {
            TempData["Error"] = $"#{order.Id} numaralı siparişin hesabı kapatılmış - iptal edilemez.";
            return RedirectToAction(nameof(Index));
        }

        if (order.Adisyon?.Payments.Count > 0)
        {
            TempData["Error"] = $"#{order.Id} numaralı siparişin hesabına ödeme alınmış - önce kasada ödeme durumunu netleştirin.";
            return RedirectToAction(nameof(Index));
        }

        if (order.Status == OrderStatus.IptalEdildi)
        {
            TempData["Error"] = $"#{order.Id} numaralı sipariş zaten iptal edilmiş.";
            return RedirectToAction(nameof(Index));
        }

        var activeItems = order.Items.Where(i => i.Status != OrderStatus.IptalEdildi).ToList();
        foreach (var item in activeItems)
        {
            item.Status = OrderStatus.IptalEdildi;
        }
        order.Status = OrderStatus.IptalEdildi;

        await _context.ExecuteInTransactionAsync(async () =>
        {
            if (!order.IsDraft)
            {
                await _inventoryService.RestoreForItemsAsync(order.Id, activeItems);
            }
        });

        await _audit.LogAsync(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            User.Identity?.Name,
            "Sipariş iptal edildi",
            entityName: "Order",
            entityId: order.Id.ToString(),
            details: $"{AdisyonLabel.For(order)}, {order.Items.Count} kalem");

        TempData["Info"] = $"#{order.Id} numaralı sipariş iptal edildi.";
        return RedirectToAction(nameof(Index));
    }
}
