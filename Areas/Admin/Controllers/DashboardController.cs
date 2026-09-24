using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestoranYonetim.Data;
using RestoranYonetim.Models;
using RestoranYonetim.Security;

namespace RestoranYonetim.Areas.Admin.Controllers;

// Yönetim özet ekranı: günlük ciro, sipariş sayısı, açık masalar, kritik stoklar, en çok satan ürünler.
[Area("Admin")]
[Authorize(Policy = Permissions.Reports.View)]
public class DashboardController : Controller
{
    private readonly ApplicationDbContext _context;

    public DashboardController(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IActionResult> Index()
    {
        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);

        var todaysClosedAdisyonlar = await _context.Adisyonlar
            .AsNoTracking()
            .Include(a => a.Orders)
            .ThenInclude(o => o.Items)
            .Where(a => a.Status == AdisyonStatus.Odendi && a.ClosedAt >= today && a.ClosedAt < tomorrow)
            .ToListAsync();

        ViewBag.TodayRevenue = todaysClosedAdisyonlar.Sum(a => a.Orders
            .Where(o => !o.IsDraft && o.Status != OrderStatus.IptalEdildi)
            .SelectMany(o => o.Items.Where(i => i.Status != OrderStatus.IptalEdildi))
            .Sum(i => i.Quantity * i.UnitPrice));
        ViewBag.TodayOrderCount = await _context.Orders.CountAsync(o => !o.IsDraft && o.SentAt >= today && o.SentAt < tomorrow);
        ViewBag.OpenTables = await _context.RestaurantTables.CountAsync(t => t.Status != TableStatus.Bos);
        ViewBag.PendingPaymentAdisyonlar = await _context.Adisyonlar
            .CountAsync(a => a.Status == AdisyonStatus.Kasada || a.Status == AdisyonStatus.KismiOdendi);
        ViewBag.ActiveWaiterCalls = await _context.WaiterCalls.CountAsync(w => !w.Resolved);
        ViewBag.PendingDiscounts = await _context.DiscountRequests.CountAsync(d => d.Status == DiscountRequestStatus.Beklemede);

        var lowStockItems = await _context.InventoryItems.AsNoTracking().ToListAsync();
        ViewBag.LowStockCount = lowStockItems.Count(i => i.IsLowStock);
        ViewBag.LowStockItems = lowStockItems.Where(i => i.IsLowStock).OrderBy(i => i.Name).Take(8).ToList();

        var last30 = DateTime.Today.AddDays(-30);
        var recentOrderItems = await _context.OrderItems
            .AsNoTracking()
            .Include(i => i.Options)
            .Where(i => i.Order != null && !i.Order.IsDraft && i.Order.SentAt >= last30 && i.Status != OrderStatus.IptalEdildi)
            .ToListAsync();
        var topProducts = recentOrderItems
            .GroupBy(i => i.MenuItemName)
            .Select(g => new { Name = g.Key, Quantity = g.Sum(x => x.Quantity), Revenue = g.Sum(x => x.LineTotal) })
            .OrderByDescending(g => g.Quantity)
            .Take(5)
            .ToList();
        ViewBag.TopProducts = topProducts;

        var openShift = await _context.CashShifts.AsNoTracking().FirstOrDefaultAsync(s => s.ClosedAt == null);
        ViewBag.OpenShift = openShift;

        return View();
    }
}
