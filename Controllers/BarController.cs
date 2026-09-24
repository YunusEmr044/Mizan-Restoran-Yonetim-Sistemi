using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestoranYonetim.Data;
using RestoranYonetim.Models;
using RestoranYonetim.Security;
using RestoranYonetim.Services;

namespace RestoranYonetim.Controllers;

// Bar ekranı - Mutfak ekranının aynısı, sadece MenuDepartment.Bar ürünleri için.
[Route("bar")]
[Authorize(Policy = Permissions.Orders.View)]
public class BarController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly RealtimeNotifier _realtime;

    public BarController(ApplicationDbContext context, RealtimeNotifier realtime)
    {
        _context = context;
        _realtime = realtime;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var items = await _context.OrderItems
            .AsNoTracking()
            .Include(i => i.MenuItem)
            .Include(i => i.Options)
            .Include(i => i.Order).ThenInclude(o => o!.Table)
            .Where(i => i.MenuItem!.Department == MenuDepartment.Bar)
            .Where(i => i.Order!.IsDraft == false)
            .Where(i => i.Status != OrderStatus.ServisEdildi && i.Status != OrderStatus.IptalEdildi)
            .OrderBy(i => i.Order!.SentAt)
            .ToListAsync();

        ViewBag.RealtimeGroups = "bar";
        return View(items);
    }

    [HttpPost("ilerlet")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Ilerlet(int id)
    {
        var item = await _context.OrderItems
            .Include(i => i.Order).ThenInclude(o => o!.Items)
            .Include(i => i.Order).ThenInclude(o => o!.Table)
            .FirstOrDefaultAsync(i => i.Id == id);
        if (item == null)
        {
            return NotFound();
        }

        var previousStatus = item.Status;
        item.Status = item.Status switch
        {
            OrderStatus.Yeni => OrderStatus.Hazirlaniyor,
            OrderStatus.Hazirlaniyor => OrderStatus.Hazir,
            _ => item.Status
        };

        if (item.Order != null)
        {
            OrderStatusHelper.Recompute(item.Order);
        }

        // "Hazır" durumuna yeni geçtiyse garsona bildirim düşür (MutfakController'daki ile aynı mantık).
        if (previousStatus != OrderStatus.Hazir && item.Status == OrderStatus.Hazir && item.Order?.TableId != null)
        {
            _context.Notifications.Add(new Notification
            {
                Message = $"{item.Order.Table?.Name ?? "Masa"}: {item.MenuItemName} hazır - servise götürülebilir",
                Link = "/Staff",
                RequiredPermission = Permissions.Orders.Create
            });
        }

        await _context.SaveChangesAsync();

        await _realtime.NotifyAsync("bar", "refresh");
        if (previousStatus != OrderStatus.Hazir && item.Status == OrderStatus.Hazir)
        {
            await _realtime.NotifyManyAsync(new[] { "staff", "notifications" }, "order-ready",
                $"{item.Order?.Table?.Name ?? "Masa"}: {item.MenuItemName} hazır");
        }

        return RedirectToAction(nameof(Index));
    }
}
