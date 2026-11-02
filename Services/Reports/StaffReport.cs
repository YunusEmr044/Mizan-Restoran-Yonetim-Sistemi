using RestoranYonetim.Models;

namespace RestoranYonetim.Services.Reports;

public sealed record WaiterOrderRow(string? UserName, OrderSource Source, int? AdisyonId, decimal LiveTotal, int CancelledQty, decimal CancelledTotal);

public sealed record DiscountRow(string? RequestedBy, decimal Amount);

public sealed record WaiterStats(string Name, int Adisyons, int Orders, decimal Revenue, decimal AvgTicket,
    int CancelledQty, decimal CancelledTotal, int Discounts, decimal DiscountTotal);

public sealed record PaymentRow(string? ReceivedBy, PaymentMethod Method, decimal Amount);

public sealed record DecisionRow(string? DecidedBy);

public sealed record CashierStats(string Name, decimal Collected, IReadOnlyList<(PaymentMethod Method, decimal Total)> ByMethod, decimal Reversed, int Decisions);

// Personel performansı: garson cirosu girilen sipariş tutarıdır (indirim ve ödemeler düşülmez); kasiyer tahsilatı ödeme kayıtlarından gelir.
public static class StaffReport
{
    public const string QrLabel = "QR (müşteri)";
    public const string UnknownLabel = "—";

    public static IReadOnlyList<WaiterStats> BuildWaiters(IEnumerable<WaiterOrderRow> orders, IEnumerable<DiscountRow> discounts)
    {
        var byWaiter = orders.ToLookup(o => o.Source == OrderSource.Musteri || string.IsNullOrWhiteSpace(o.UserName) ? QrLabel : o.UserName!.Trim());
        var discountsBy = discounts.Where(d => !string.IsNullOrWhiteSpace(d.RequestedBy)).ToLookup(d => d.RequestedBy!.Trim());

        return byWaiter.Select(g => g.Key).Union(discountsBy.Select(g => g.Key))
            .Select(name =>
            {
                var mine = byWaiter[name].ToList();
                var revenue = mine.Sum(o => o.LiveTotal);
                var adisyons = mine.Where(o => o.AdisyonId != null).Select(o => o.AdisyonId).Distinct().Count();
                var myDiscounts = discountsBy[name].ToList();
                return new WaiterStats(name, adisyons, mine.Count, revenue, adisyons == 0 ? 0m : Math.Round(revenue / adisyons, 2),
                    mine.Sum(o => o.CancelledQty), mine.Sum(o => o.CancelledTotal), myDiscounts.Count, myDiscounts.Sum(d => d.Amount));
            })
            .OrderByDescending(w => w.Revenue).ThenBy(w => w.Name)
            .ToList();
    }

    public static IReadOnlyList<CashierStats> BuildCashiers(IEnumerable<PaymentRow> payments, IEnumerable<DecisionRow> decisions)
    {
        var byCashier = payments.ToLookup(p => Name(p.ReceivedBy));
        var decisionsBy = decisions.ToLookup(d => Name(d.DecidedBy));

        return byCashier.Select(g => g.Key).Union(decisionsBy.Select(g => g.Key))
            .Select(name =>
            {
                var mine = byCashier[name].ToList();
                var positive = mine.Where(p => p.Amount > 0).ToList();
                var byMethod = positive.GroupBy(p => p.Method).OrderBy(g => g.Key).Select(g => (g.Key, g.Sum(p => p.Amount))).ToList();
                return new CashierStats(name, positive.Sum(p => p.Amount), byMethod, -mine.Where(p => p.Amount < 0).Sum(p => p.Amount), decisionsBy[name].Count());
            })
            .OrderByDescending(c => c.Collected).ThenBy(c => c.Name)
            .ToList();
    }

    private static string Name(string? value) => string.IsNullOrWhiteSpace(value) ? UnknownLabel : value.Trim();
}
