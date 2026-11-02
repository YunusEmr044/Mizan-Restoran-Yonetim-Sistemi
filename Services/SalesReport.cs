using Microsoft.EntityFrameworkCore;
using RestoranYonetim.Data;
using RestoranYonetim.Models;
using RestoranYonetim.Models.Reports;

namespace RestoranYonetim.Services;

public sealed record SalesSummary(List<DailySalesRow> Daily, decimal TotalRevenue, decimal TotalRefunds, int TotalCount, decimal AvgTicket,
    IReadOnlyList<(OrderType Type, int Count, decimal Total)> ByType);

// Satış, adisyonun kapandığı güne; iade, yapıldığı güne yazılır. Böylece sonradan yapılan iade geçmiş günün cirosunu değiştirmez.
public static class SalesReport
{
    // İade, belirli bir ödemeyi geri almayan eksi kayıttır; ödeme iptali ReversesPaymentId taşır ve satış tutarının parçası sayılır.
    public static bool IsRefund(decimal amount, int? reversesPaymentId) => amount < 0 && reversesPaymentId == null;

    public static async Task<SalesSummary> LoadAsync(ApplicationDbContext context, DateTime start, DateTime end)
    {
        var closed = await context.Adisyonlar
            .AsNoTracking()
            .Where(a => (a.Status == AdisyonStatus.Odendi || a.Status == AdisyonStatus.Iade) && a.ClosedAt >= start && a.ClosedAt < end)
            .Select(a => new { ClosedAt = a.ClosedAt!.Value, a.OrderType, Payments = a.Payments.Select(p => new { p.Amount, p.ReversesPaymentId }).ToList() })
            .ToListAsync();

        var negativePayments = await context.Payments
            .AsNoTracking()
            .Where(p => p.Amount < 0 && p.CreatedAt >= start && p.CreatedAt < end)
            .Select(p => new { p.CreatedAt, p.Amount, p.ReversesPaymentId })
            .ToListAsync();

        return Build(
            closed.Select(a => (a.ClosedAt, a.Payments.Where(p => !IsRefund(p.Amount, p.ReversesPaymentId)).Sum(p => p.Amount), a.OrderType)),
            negativePayments.Where(p => IsRefund(p.Amount, p.ReversesPaymentId)).Select(p => (p.CreatedAt, -p.Amount)));
    }

    public static SalesSummary Build(IEnumerable<(DateTime ClosedAt, decimal Sales, OrderType Type)> sales, IEnumerable<(DateTime At, decimal Amount)> refunds)
    {
        var saleList = sales.ToList();
        var refundList = refunds.ToList();

        var salesByDay = saleList.ToLookup(s => s.ClosedAt.Date);
        var refundsByDay = refundList.ToLookup(r => r.At.Date);
        var daily = salesByDay.Select(g => g.Key)
            .Union(refundsByDay.Select(g => g.Key))
            .OrderBy(d => d)
            .Select(date =>
            {
                var dayRefunds = refundsByDay[date].Sum(r => r.Amount);
                return new DailySalesRow { Date = date, Count = salesByDay[date].Count(), Refunds = dayRefunds, Total = salesByDay[date].Sum(s => s.Sales) - dayRefunds };
            })
            .ToList();

        var grossSales = saleList.Sum(s => s.Sales);
        var totalRefunds = refundList.Sum(r => r.Amount);
        var avgTicket = saleList.Count > 0 ? grossSales / saleList.Count : 0m;

        var byType = saleList.GroupBy(s => s.Type).OrderBy(g => g.Key)
            .Select(g => (g.Key, g.Count(), g.Sum(s => s.Sales))).ToList();

        return new SalesSummary(daily, grossSales - totalRefunds, totalRefunds, saleList.Count, avgTicket, byType);
    }
}
