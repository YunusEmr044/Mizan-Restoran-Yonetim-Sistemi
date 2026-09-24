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
[Authorize(Policy = Permissions.Reports.View)]
public class OrdersController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly AuditService _audit;

    public OrdersController(ApplicationDbContext context, AuditService audit)
    {
        _context = context;
        _audit = audit;
    }

    public async Task<IActionResult> Index()
    {
        var orders = await _context.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .Include(o => o.Table)
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
        var order = await _context.Orders.Include(o => o.Items).Include(o => o.Table).FirstOrDefaultAsync(o => o.Id == id);
        if (order == null)
        {
            return NotFound();
        }

        foreach (var item in order.Items.Where(i => i.Status != OrderStatus.IptalEdildi))
        {
            item.Status = OrderStatus.IptalEdildi;
        }
        OrderStatusHelper.Recompute(order);
        order.Status = OrderStatus.IptalEdildi;
        await _context.SaveChangesAsync();

        await _audit.LogAsync(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            User.Identity?.Name,
            "Sipariş iptal edildi",
            entityName: "Order",
            entityId: order.Id.ToString(),
            details: $"Masa: {order.Table?.Name}, {order.Items.Count} kalem");

        TempData["Info"] = $"#{order.Id} numaralı sipariş iptal edildi.";
        return RedirectToAction(nameof(Index));
    }
}
