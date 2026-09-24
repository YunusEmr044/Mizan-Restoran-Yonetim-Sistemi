using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestoranYonetim.Data;
using RestoranYonetim.Models;
using RestoranYonetim.Models.Reports;
using RestoranYonetim.Security;
using RestoranYonetim.Services;

namespace RestoranYonetim.Areas.Admin.Controllers;

// Tarih aralığı seçilebilir temel raporlar: satış, ürün bazlı satış, kasa vardiya geçmişi.
[Area("Admin")]
[Authorize(Policy = Permissions.Reports.View)]
public class ReportsController : Controller
{
    private readonly ApplicationDbContext _context;

    public ReportsController(ApplicationDbContext context)
    {
        _context = context;
    }

    public IActionResult Index() => View();

    public async Task<IActionResult> Satis(DateTime? start, DateTime? end)
    {
        var (rangeStart, rangeEnd) = ResolveRange(start, end, defaultDaysBack: 6);
        ViewBag.Start = rangeStart;
        ViewBag.End = rangeEnd.AddDays(-1);

        var (daily, paymentBreakdown, totalRevenue, totalCount, avgTicket) = await BuildSatisRaporu(rangeStart, rangeEnd);

        ViewBag.TotalRevenue = totalRevenue;
        ViewBag.TotalCount = totalCount;
        ViewBag.AvgTicket = avgTicket;
        ViewBag.Daily = daily;
        ViewBag.PaymentBreakdown = paymentBreakdown;

        return View();
    }

    public async Task<IActionResult> SatisExport(DateTime? start, DateTime? end)
    {
        var (rangeStart, rangeEnd) = ResolveRange(start, end, defaultDaysBack: 6);
        var (daily, _, _, _, _) = await BuildSatisRaporu(rangeStart, rangeEnd);

        var csv = CsvExporter.Build(
            new[] { "Tarih", "Ciro", "Adisyon Sayısı" },
            daily.Select(d => (IEnumerable<object?>)new object?[] { d.Date.ToString("dd.MM.yyyy"), d.Total.ToString("0.00"), d.Count }));

        return File(csv, "text/csv", $"satis-raporu-{rangeStart:yyyyMMdd}-{rangeEnd.AddDays(-1):yyyyMMdd}.csv");
    }

    public async Task<IActionResult> Urunler(DateTime? start, DateTime? end)
    {
        var (rangeStart, rangeEnd) = ResolveRange(start, end, defaultDaysBack: 29);
        ViewBag.Start = rangeStart;
        ViewBag.End = rangeEnd.AddDays(-1);

        var productRows = await BuildUrunlerRaporu(rangeStart, rangeEnd);
        return View(productRows);
    }

    public async Task<IActionResult> UrunlerExport(DateTime? start, DateTime? end)
    {
        var (rangeStart, rangeEnd) = ResolveRange(start, end, defaultDaysBack: 29);
        var productRows = await BuildUrunlerRaporu(rangeStart, rangeEnd);

        var csv = CsvExporter.Build(
            new[] { "Ürün", "Adet", "Ciro" },
            productRows.Select(p => (IEnumerable<object?>)new object?[] { p.Name, p.Quantity, p.Revenue.ToString("0.00") }));

        return File(csv, "text/csv", $"urun-raporu-{rangeStart:yyyyMMdd}-{rangeEnd.AddDays(-1):yyyyMMdd}.csv");
    }

    public async Task<IActionResult> Kasa(DateTime? start, DateTime? end)
    {
        var shifts = await BuildKasaRaporu(start, end);
        ViewBag.Start = start;
        ViewBag.End = end;
        return View(shifts);
    }

    public async Task<IActionResult> KasaExport(DateTime? start, DateTime? end)
    {
        var shifts = await BuildKasaRaporu(start, end);

        var csv = CsvExporter.Build(
            new[] { "Açılış", "Kapanış", "Açan", "Kapatan", "Beklenen", "Sayılan", "Fark" },
            shifts.Select(s => (IEnumerable<object?>)new object?[]
            {
                s.OpenedAt.ToString("dd.MM.yyyy HH:mm"),
                s.ClosedAt.HasValue ? s.ClosedAt.Value.ToString("dd.MM.yyyy HH:mm") : "Açık",
                s.OpenedByUserName,
                s.ClosedByUserName,
                s.ExpectedCash.ToString("0.00"),
                s.ClosingCountedAmount.HasValue ? s.ClosingCountedAmount.Value.ToString("0.00") : "",
                s.CashDifference.HasValue ? s.CashDifference.Value.ToString("0.00") : ""
            }));

        var suffix = start.HasValue || end.HasValue
            ? $"{(start ?? DateTime.Today.AddYears(-5)):yyyyMMdd}-{(end ?? DateTime.Today):yyyyMMdd}"
            : "son-60-vardiya";
        return File(csv, "text/csv", $"kasa-raporu-{suffix}.csv");
    }

    // start/end verilmezse varsayılan olarak "son N gün" aralığını döner.
    private static (DateTime Start, DateTime End) ResolveRange(DateTime? start, DateTime? end, int defaultDaysBack)
    {
        var rangeStart = (start ?? DateTime.Today.AddDays(-defaultDaysBack)).Date;
        var rangeEnd = (end ?? DateTime.Today).Date.AddDays(1);
        return (rangeStart, rangeEnd);
    }

    private async Task<(List<DailySalesRow> Daily, List<PaymentBreakdownRow> PaymentBreakdown, decimal TotalRevenue, int TotalCount, decimal AvgTicket)> BuildSatisRaporu(DateTime rangeStart, DateTime rangeEnd)
    {
        var adisyonlar = await _context.Adisyonlar
            .AsNoTracking()
            .Include(a => a.Orders)
            .ThenInclude(o => o.Items)
            .Where(a => a.Status == AdisyonStatus.Odendi && a.ClosedAt >= rangeStart && a.ClosedAt < rangeEnd)
            .ToListAsync();

        var revenueByAdisyon = adisyonlar.ToDictionary(a => a.Id, a => a.Orders
            .Where(o => !o.IsDraft && o.Status != OrderStatus.IptalEdildi)
            .SelectMany(o => o.Items.Where(i => i.Status != OrderStatus.IptalEdildi))
            .Sum(i => i.Quantity * i.UnitPrice));

        var totalRevenue = revenueByAdisyon.Values.Sum();
        var totalCount = adisyonlar.Count;
        var avgTicket = revenueByAdisyon.Count > 0 ? revenueByAdisyon.Values.Average() : 0m;

        var daily = adisyonlar
            .GroupBy(a => a.ClosedAt!.Value.Date)
            .Select(g => new DailySalesRow { Date = g.Key, Total = g.Sum(a => revenueByAdisyon[a.Id]), Count = g.Count() })
            .OrderBy(g => g.Date)
            .ToList();

        var paymentBreakdown = await _context.Payments
            .AsNoTracking()
            .Where(p => p.CreatedAt >= rangeStart && p.CreatedAt < rangeEnd)
            .GroupBy(p => p.Method)
            .Select(g => new PaymentBreakdownRow { Method = g.Key, Total = g.Sum(p => p.Amount) })
            .ToListAsync();

        return (daily, paymentBreakdown, totalRevenue, totalCount, avgTicket);
    }

    private async Task<List<ProductSalesRow>> BuildUrunlerRaporu(DateTime rangeStart, DateTime rangeEnd)
    {
        var products = await _context.OrderItems
            .AsNoTracking()
            .Include(i => i.Order)
            .Where(i => i.Order != null && !i.Order.IsDraft && i.Order.SentAt >= rangeStart && i.Order.SentAt < rangeEnd
                && i.Status != OrderStatus.IptalEdildi)
            .ToListAsync();

        return products
            .GroupBy(i => i.MenuItemName)
            .Select(g => new ProductSalesRow
            {
                Name = g.Key,
                Quantity = g.Sum(x => x.Quantity),
                Revenue = g.Sum(x => x.Quantity * x.UnitPrice)
            })
            .OrderByDescending(g => g.Quantity)
            .ToList();
    }

    // Tarih filtresi verilmezse son 60 vardiya döner; filtre verilirse sınırsız döner.
    private async Task<List<CashShift>> BuildKasaRaporu(DateTime? start, DateTime? end)
    {
        var query = _context.CashShifts.AsNoTracking().Include(s => s.Payments).AsQueryable();
        var hasFilter = start.HasValue || end.HasValue;

        if (start.HasValue)
        {
            query = query.Where(s => s.OpenedAt >= start.Value.Date);
        }
        if (end.HasValue)
        {
            query = query.Where(s => s.OpenedAt < end.Value.Date.AddDays(1));
        }

        query = query.OrderByDescending(s => s.OpenedAt);

        return hasFilter ? await query.ToListAsync() : await query.Take(60).ToListAsync();
    }
}
