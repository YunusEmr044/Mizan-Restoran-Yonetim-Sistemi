using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestoranYonetim.Data;
using RestoranYonetim.Models;
using RestoranYonetim.Security;
using RestoranYonetim.Services;

namespace RestoranYonetim.Controllers;

// Mutfak ekranı: gönderilmiş (taslak olmayan) siparişlerin mutfağa ait kalemlerini
// gösterir, bekleme süresini vurgular, kalem bazlı durum ilerletmeyi sağlar.
[Route("mutfak")]
[Authorize(Policy = Permissions.Orders.View)]
public class MutfakController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly RealtimeNotifier _realtime;

    public MutfakController(ApplicationDbContext context, RealtimeNotifier realtime)
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
            .Where(i => i.MenuItem!.Department == MenuDepartment.Mutfak)
            .Where(i => i.Order!.IsDraft == false)
            .Where(i => i.Status != OrderStatus.ServisEdildi && i.Status != OrderStatus.IptalEdildi)
            .OrderBy(i => i.Order!.SentAt)
            .ToListAsync();

        ViewBag.RealtimeGroups = "mutfak";
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

        // "Hazır" durumuna yeni geçtiyse garsona bildirim düşür.
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

        await _realtime.NotifyAsync("mutfak", "refresh");
        if (previousStatus != OrderStatus.Hazir && item.Status == OrderStatus.Hazir)
        {
            await _realtime.NotifyManyAsync(new[] { "staff", "notifications" }, "order-ready",
                $"{item.Order?.Table?.Name ?? "Masa"}: {item.MenuItemName} hazır");
        }

        return RedirectToAction(nameof(Index));
    }
}
