using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestoranYonetim.Data;
using RestoranYonetim.Extensions;
using RestoranYonetim.Models;
using RestoranYonetim.Models.Reports;
using RestoranYonetim.Security;
using RestoranYonetim.Services;
using RestoranYonetim.Services.Reports;

namespace RestoranYonetim.Areas.Admin.Controllers;

// Tarih aralığı seçilebilir raporlar: satış, ürün bazlı satış, kasa vardiya geçmişi, personel performansı, dönem karşılaştırması ve kayıplar.
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

        var (summary, paymentBreakdown) = await BuildSatisRaporu(rangeStart, rangeEnd);

        ViewBag.TotalRevenue = summary.TotalRevenue;
        ViewBag.TotalRefunds = summary.TotalRefunds;
        ViewBag.TotalCount = summary.TotalCount;
        ViewBag.AvgTicket = summary.AvgTicket;
        ViewBag.Daily = summary.Daily;
        ViewBag.PaymentBreakdown = paymentBreakdown;
        ViewBag.ByType = summary.ByType;

        return View();
    }

    public async Task<IActionResult> SatisExport(DateTime? start, DateTime? end)
    {
        var (rangeStart, rangeEnd) = ResolveRange(start, end, defaultDaysBack: 6);
        var (summary, _) = await BuildSatisRaporu(rangeStart, rangeEnd);

        var csv = CsvExporter.Build(
            new[] { "Tarih", "Ciro", "İade", "Adisyon Sayısı" },
            summary.Daily.Select(d => (IEnumerable<object?>)new object?[] { d.Date.ToString("dd.MM.yyyy"), d.Total.ToString("0.00"), d.Refunds.ToString("0.00"), d.Count }));

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
        var from = (start ?? DateTime.Today.AddDays(-defaultDaysBack)).Date;
        var to = (end ?? DateTime.Today).Date;
        // Ters girilen aralık (bitiş başlangıçtan önce) yer değiştirilir; aralık en az bir gündür.
        return from <= to ? (from, to.AddDays(1)) : (to, from.AddDays(1));
    }

    public async Task<IActionResult> Personel(DateTime? start, DateTime? end)
    {
        var (rangeStart, rangeEnd) = ResolveRange(start, end, defaultDaysBack: 6);
        return View(await BuildPersonelRaporu(rangeStart, rangeEnd));
    }

    public async Task<IActionResult> PersonelExport(DateTime? start, DateTime? end)
    {
        var (rangeStart, rangeEnd) = ResolveRange(start, end, defaultDaysBack: 6);
        var report = await BuildPersonelRaporu(rangeStart, rangeEnd);

        var rows = report.Waiters.Select(w => (IEnumerable<object?>)new object?[]
        {
            w.Name, w.Adisyons, w.Orders, w.Revenue.ToString("0.00"), w.AvgTicket.ToString("0.00"),
            w.CancelledQty, w.CancelledTotal.ToString("0.00"), w.Discounts, w.DiscountTotal.ToString("0.00")
        }).ToList();
        if (report.Cashiers != null)
        {
            rows.Add(Array.Empty<object?>());
            rows.Add(new object?[] { "Kasiyer", "Tahsilat", "Yöntem dağılımı", "İade / iptal", "Onayladığı talep" });
            rows.AddRange(report.Cashiers.Select(c => (IEnumerable<object?>)new object?[]
            {
                c.Name, c.Collected.ToString("0.00"), string.Join(" · ", c.ByMethod.Select(m => $"{m.Method.Label()} {m.Total:0.00}")),
                c.Reversed.ToString("0.00"), c.Decisions
            }));
        }

        var csv = CsvExporter.Build(
            new[] { "Garson", "Adisyon", "Sipariş", "Kalem cirosu", "Ortalama fiş", "İptal adet", "İptal tutarı", "İndirim adet", "İndirim tutarı" },
            rows);
        return File(csv, "text/csv", $"personel-raporu-{rangeStart:yyyyMMdd}-{rangeEnd.AddDays(-1):yyyyMMdd}.csv");
    }

    public async Task<IActionResult> Karsilastirma(DateTime? start, DateTime? end)
    {
        var (rangeStart, rangeEnd) = ResolveRange(start, end, defaultDaysBack: 6);
        var (previousStart, previousEnd) = PeriodComparison.Previous(rangeStart, rangeEnd);
        var days = (int)(rangeEnd - rangeStart).TotalDays;

        var current = await SalesReport.LoadAsync(_context, rangeStart, rangeEnd);
        var previous = await SalesReport.LoadAsync(_context, previousStart, previousEnd);
        var chart = new LineChartModel(
            Enumerable.Range(0, days).Select(i => rangeStart.AddDays(i)).ToList(),
            PeriodComparison.Series(current.Daily, rangeStart, days),
            PeriodComparison.Series(previous.Daily, previousStart, days),
            "Bu dönem", "Önceki dönem");

        var orders = await LoadWaiterOrdersAsync(rangeStart, rangeEnd);
        var discounts = await ApprovedDiscounts(rangeStart, rangeEnd).Select(d => new { d.Amount, d.Reason }).ToListAsync();
        var negatives = await _context.Payments.AsNoTracking()
            .Where(p => p.Amount < 0 && p.CreatedAt >= rangeStart && p.CreatedAt < rangeEnd)
            .Select(p => new { p.Amount, p.ReversesPaymentId })
            .ToListAsync();
        var refunds = negatives.Where(p => SalesReport.IsRefund(p.Amount, p.ReversesPaymentId)).ToList();
        var reversals = negatives.Where(p => p.ReversesPaymentId != null).ToList();

        var losses = LossReport.Build(
            (orders.Sum(o => o.CancelledQty), orders.Sum(o => o.CancelledTotal)),
            (discounts.Count, discounts.Sum(d => d.Amount)),
            (refunds.Count, -refunds.Sum(p => p.Amount)),
            (reversals.Count, -reversals.Sum(p => p.Amount)),
            current.TotalRevenue);

        var voidReasons = await _context.VoidRequests.AsNoTracking()
            .Where(v => v.Type == VoidRequestType.KalemIptali && v.Status == DiscountRequestStatus.Onaylandi && v.DecidedAt >= rangeStart && v.DecidedAt < rangeEnd)
            .Select(v => new
            {
                v.Reason,
                Amount = v.OrderItem == null ? 0m : (v.OrderItem.UnitPrice + v.OrderItem.Options.Sum(o => o.ExtraPrice)) * v.OrderItem.Quantity
            })
            .ToListAsync();
        var reasons = LossReport.TopReasons(
            voidReasons.Select(v => new ReasonRow(ReasonKind.Iptal, v.Reason, v.Amount))
                .Concat(discounts.Select(d => new ReasonRow(ReasonKind.Indirim, d.Reason, d.Amount))));

        return View(new ComparisonView(rangeStart, rangeEnd.AddDays(-1), previousStart, previousEnd.AddDays(-1), current, previous, chart, losses, reasons));
    }

    private async Task<StaffReportView> BuildPersonelRaporu(DateTime rangeStart, DateTime rangeEnd)
    {
        var orders = await LoadWaiterOrdersAsync(rangeStart, rangeEnd);
        var discounts = await ApprovedDiscounts(rangeStart, rangeEnd).Select(d => new DiscountRow(d.RequestedByUserName, d.Amount)).ToListAsync();
        var waiters = StaffReport.BuildWaiters(orders, discounts);

        IReadOnlyList<CashierStats>? cashiers = null;
        if (User.HasClaim(Permissions.ClaimType, Permissions.Cash.ViewReports))
        {
            var payments = await _context.Payments.AsNoTracking()
                .Where(p => p.CreatedAt >= rangeStart && p.CreatedAt < rangeEnd)
                .Select(p => new PaymentRow(p.ReceivedByUserName, p.Method, p.Amount))
                .ToListAsync();
            var discountDeciders = await ApprovedDiscounts(rangeStart, rangeEnd).Select(d => d.DecidedByUserName).ToListAsync();
            var voidDeciders = await _context.VoidRequests.AsNoTracking()
                .Where(v => v.Status == DiscountRequestStatus.Onaylandi && v.DecidedAt >= rangeStart && v.DecidedAt < rangeEnd)
                .Select(v => v.DecidedByUserName)
                .ToListAsync();
            cashiers = StaffReport.BuildCashiers(payments, discountDeciders.Concat(voidDeciders).Select(d => new DecisionRow(d)));
        }

        return new StaffReportView(rangeStart, rangeEnd.AddDays(-1), waiters, cashiers);
    }

    // Gönderilmiş siparişler, gönderildiği güne göre; kalem tutarı seçenek farklarıyla birlikte hesaplanır.
    private async Task<List<WaiterOrderRow>> LoadWaiterOrdersAsync(DateTime rangeStart, DateTime rangeEnd)
    {
        var items = await _context.OrderItems.AsNoTracking()
            .Where(i => !i.Order!.IsDraft && i.Order.SentAt >= rangeStart && i.Order.SentAt < rangeEnd)
            .Select(i => new
            {
                i.OrderId,
                i.Order!.CreatedByUserName,
                i.Order.Source,
                i.Order.AdisyonId,
                Cancelled = i.Status == OrderStatus.IptalEdildi,
                i.Quantity,
                Line = (i.UnitPrice + i.Options.Sum(o => o.ExtraPrice)) * i.Quantity
            })
            .ToListAsync();

        return items.GroupBy(i => i.OrderId)
            .Select(g =>
            {
                var first = g.First();
                var cancelled = g.Where(i => i.Cancelled).ToList();
                return new WaiterOrderRow(first.CreatedByUserName, first.Source, first.AdisyonId,
                    g.Where(i => !i.Cancelled).Sum(i => i.Line), cancelled.Sum(i => i.Quantity), cancelled.Sum(i => i.Line));
            })
            .ToList();
    }

    private IQueryable<DiscountRequest> ApprovedDiscounts(DateTime rangeStart, DateTime rangeEnd) =>
        _context.DiscountRequests.AsNoTracking()
            .Where(d => d.Status == DiscountRequestStatus.Onaylandi && d.DecidedAt >= rangeStart && d.DecidedAt < rangeEnd);

    private async Task<(SalesSummary Summary, List<PaymentBreakdownRow> PaymentBreakdown)> BuildSatisRaporu(DateTime rangeStart, DateTime rangeEnd)
    {
        var summary = await SalesReport.LoadAsync(_context, rangeStart, rangeEnd);

        var paymentBreakdown = await _context.Payments
            .AsNoTracking()
            .Where(p => p.CreatedAt >= rangeStart && p.CreatedAt < rangeEnd)
            .GroupBy(p => p.Method)
            .Select(g => new PaymentBreakdownRow { Method = g.Key, Total = g.Sum(p => p.Amount) })
            .ToListAsync();

        return (summary, paymentBreakdown);
    }

    private async Task<List<ProductSalesRow>> BuildUrunlerRaporu(DateTime rangeStart, DateTime rangeEnd)
    {
        var products = await _context.OrderItems
            .AsNoTracking()
            .Include(i => i.Order)
            .Include(i => i.Options)
            .Where(i => i.Order != null && !i.Order.IsDraft && i.Order.SentAt >= rangeStart && i.Order.SentAt < rangeEnd
                && i.Status != OrderStatus.IptalEdildi)
            .ToListAsync();

        return products
            .GroupBy(i => i.MenuItemName)
            .Select(g => new ProductSalesRow
            {
                Name = g.Key,
                Quantity = g.Sum(x => x.Quantity),
                Revenue = g.Sum(x => x.LineTotal)
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
