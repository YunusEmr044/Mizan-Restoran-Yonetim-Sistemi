using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RestoranYonetim.Data;
using RestoranYonetim.Models;
using RestoranYonetim.Security;
using RestoranYonetim.Services;
using RestoranYonetim.Services.Fiscal;
using RestoranYonetim.Services.Printing;

namespace RestoranYonetim.Areas.Admin.Controllers;

public record TopProductRow(string Name, int Quantity, decimal Revenue);

// Kart değeri null ise kullanıcının o kart için yetkisi yoktur ve kart gösterilmez.
public class DashboardViewModel
{
    public decimal TodayRevenue { get; set; }
    public int TodayOrderCount { get; set; }
    public int OpenTables { get; set; }
    public bool CanManageTables { get; set; }
    public List<TopProductRow> TopProducts { get; set; } = new();
    public CashShift? OpenShift { get; set; }
    public bool ShowCash { get; set; }
    public int? PendingPayment { get; set; }
    public int? ActiveWaiterCalls { get; set; }
    public int? PendingDiscounts { get; set; }
    public int? PendingVoids { get; set; }
    public int? LowStockCount { get; set; }
    public List<InventoryItem> LowStockItems { get; set; } = new();
    public int? TodayReservationCount { get; set; }
    public List<Reservation> NextReservations { get; set; } = new();
    public int? OpenPackages { get; set; }
    public int? PrintFailures { get; set; }
    public int? FiscalPending { get; set; }
    public int? PendingLeaves { get; set; }
    public string? PreviewRole { get; set; }
    public string? PreviewLanding { get; set; }
    public bool PreviewBlocked { get; set; }
    public IReadOnlyList<SetupStep>? SetupSteps { get; set; }
    public int? FeedbackCount { get; set; }
    public double? FeedbackAverage { get; set; }
}

// Yönetim özet ekranı: günlük ciro, sipariş sayısı, açık masalar, bekleyen talepler, rezervasyonlar, geri bildirimler, kritik stoklar, en çok satan ürünler.
[Area("Admin")]
[Authorize(Policy = Permissions.Reports.View)]
public class DashboardController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly PrintFailureLog _printFailures;
    private readonly IMemoryCache _cache;
    private readonly IConfiguration _configuration;
    private ClaimsPrincipal _viewer = new();

    public DashboardController(ApplicationDbContext context, PrintFailureLog printFailures, IMemoryCache cache, IConfiguration configuration)
    {
        _context = context;
        _printFailures = printFailures;
        _cache = cache;
        _configuration = configuration;
    }

    private bool Has(string permission) => _viewer.HasClaim(Permissions.ClaimType, permission);

    public async Task<IActionResult> Index()
    {
        _viewer = await NavPreview.PreviewPrincipalAsync(HttpContext, _context) ?? User;
        if (!Has(Permissions.Reports.View))
        {
            var permissions = _viewer.FindAll(Permissions.ClaimType).Select(c => c.Value).ToHashSet();
            return View(new DashboardViewModel { PreviewBlocked = true, PreviewRole = NavPreview.RoleFor(HttpContext), PreviewLanding = RolePermissions.OperationalLanding(permissions) });
        }

        var today = DateTime.Today;
        var tomorrow = today.AddDays(1);
        var model = new DashboardViewModel
        {
            TodayRevenue = (await SalesReport.LoadAsync(_context, today, tomorrow)).TotalRevenue,
            TodayOrderCount = await _context.Orders.CountAsync(o => !o.IsDraft && o.SentAt >= today && o.SentAt < tomorrow),
            OpenTables = await _context.RestaurantTables.CountAsync(t => t.Status != TableStatus.Bos),
            CanManageTables = Has(Permissions.Tables.Manage)
        };

        var last30 = today.AddDays(-30);
        var recentOrderItems = await _context.OrderItems
            .AsNoTracking()
            .Include(i => i.Options)
            .Where(i => i.Order != null && !i.Order.IsDraft && i.Order.SentAt >= last30 && i.Status != OrderStatus.IptalEdildi)
            .ToListAsync();
        model.TopProducts = recentOrderItems
            .GroupBy(i => i.MenuItemName)
            .Select(g => new TopProductRow(g.Key, g.Sum(x => x.Quantity), g.Sum(x => x.LineTotal)))
            .OrderByDescending(g => g.Quantity)
            .Take(5)
            .ToList();

        if (Has(Permissions.Cash.View))
        {
            model.ShowCash = true;
            model.OpenShift = await _context.CashShifts.AsNoTracking().FirstOrDefaultAsync(s => s.ClosedAt == null);
            model.PendingPayment = await _context.Adisyonlar
                .CountAsync(a => a.Status == AdisyonStatus.Kasada || a.Status == AdisyonStatus.KismiOdendi);
        }
        if (Has(Permissions.Orders.View))
        {
            model.ActiveWaiterCalls = await _context.WaiterCalls.CountAsync(w => !w.Resolved);
            var packages = await _context.Adisyonlar.CountAsync(a => a.OrderType != OrderType.Masa && Adisyon.ActiveStatuses.Contains(a.Status));
            model.OpenPackages = packages > 0 ? packages : null;
        }
        if (Has(Permissions.Printers.Manage))
        {
            var failures = _printFailures.Recent(MenuDepartment.Mutfak, DateTime.Now).Count + _printFailures.Recent(MenuDepartment.Bar, DateTime.Now).Count;
            model.PrintFailures = failures > 0 ? failures : null;
        }
        if (Has(Permissions.Cash.View))
        {
            var fiscal = await _context.FiscalReceipts.CountAsync(r => FiscalReceiptRules.OpenStatuses.Contains(r.Status));
            model.FiscalPending = fiscal > 0 ? fiscal : null;
        }
        if (Has(Permissions.Cash.ApprovePayment))
        {
            model.PendingDiscounts = await _context.DiscountRequests.CountAsync(d => d.Status == DiscountRequestStatus.Beklemede);
        }
        if (Has(Permissions.Cash.ApproveRefund))
        {
            model.PendingVoids = await _context.VoidRequests.CountAsync(v => v.Status == DiscountRequestStatus.Beklemede);
        }
        if (Has(Permissions.Inventory.View))
        {
            var inventory = await _context.InventoryItems.AsNoTracking().ToListAsync();
            var low = inventory.Where(i => i.IsLowStock).OrderBy(i => i.Name).ToList();
            model.LowStockCount = low.Count;
            model.LowStockItems = low.Take(8).ToList();
        }
        if (Has(Permissions.Reservations.View))
        {
            var reservations = _context.Reservations.AsNoTracking()
                .Where(r => r.Date == today && (r.Status == ReservationStatus.Beklemede || r.Status == ReservationStatus.Onaylandi));
            model.TodayReservationCount = await reservations.CountAsync();
            model.NextReservations = await reservations.OrderBy(r => r.Time).Take(3).ToListAsync();
        }
        if (Has(Permissions.Feedback.View))
        {
            var week = _context.Feedbacks.AsNoTracking().Where(f => f.CreatedAt >= today.AddDays(-6));
            model.FeedbackCount = await week.CountAsync();
            model.FeedbackAverage = model.FeedbackCount > 0 ? await week.AverageAsync(f => (double)f.Rating) : null;
        }

        if (Has(Permissions.Staff.Manage))
        {
            var leaves = await _context.LeaveRequests.CountAsync(l => l.Status == LeaveStatus.Beklemede);
            model.PendingLeaves = leaves > 0 ? leaves : null;
        }
        if (Has(Permissions.Settings.Manage))
        {
            var steps = await SetupChecklistLoader.LoadAsync(_context, _cache, _configuration);
            model.SetupSteps = SetupChecklist.IsComplete(steps) ? null : steps;
        }

        model.PreviewRole = NavPreview.RoleFor(HttpContext);
        return View(model);
    }
}
