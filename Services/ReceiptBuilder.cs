using Microsoft.EntityFrameworkCore;
using RestoranYonetim.Data;
using RestoranYonetim.Extensions;
using RestoranYonetim.Models;

namespace RestoranYonetim.Services;

public sealed record ReceiptView(
    Adisyon Adisyon,
    SiteContent Site,
    ReceiptPaper Paper,
    int FontPx,
    string? AreaName,
    string? Waiters,
    IReadOnlyList<(string Method, decimal Amount)> Payments,
    IReadOnlyList<OrderItem> Items,
    DateTime PrintedAt);

// Kasa ve garson ekranındaki "Yazdır" aynı adisyon çıktısını üretir.
public static class ReceiptBuilder
{
    public static async Task<ReceiptView?> LoadAsync(ApplicationDbContext context, int adisyonId)
    {
        var adisyon = await context.Adisyonlar
            .AsNoTracking()
            .Include(a => a.Table).ThenInclude(t => t!.Area)
            .IncludeTotals()
            .FirstOrDefaultAsync(a => a.Id == adisyonId);
        if (adisyon == null)
        {
            return null;
        }

        var site = await context.SiteContents.AsNoTracking().FirstOrDefaultAsync() ?? new SiteContent();
        var sentOrders = adisyon.Orders.Where(o => !o.IsDraft && o.Status != OrderStatus.IptalEdildi).OrderBy(o => o.SentAt).ToList();
        var waiterIds = sentOrders.Where(o => o.Source == OrderSource.Garson).Select(o => o.CreatedByUserId).ToList();
        var idSet = waiterIds.Where(id => id != null).Distinct().ToList();
        var fullNames = await context.Users.AsNoTracking()
            .Where(u => idSet.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName);

        return new ReceiptView(
            adisyon,
            site,
            ReceiptLayout.Paper(site.ReceiptPaperSize),
            ReceiptLayout.FontPx(site.ReceiptPaperSize, site.ReceiptFontSize),
            adisyon.Table?.Area?.Name,
            WaiterNames(waiterIds, fullNames),
            PaymentLines(adisyon.Payments),
            sentOrders.SelectMany(o => o.Items).Where(i => i.Status != OrderStatus.IptalEdildi).ToList(),
            DateTime.Now);
    }

    public static IReadOnlyList<(string Method, decimal Amount)> PaymentLines(IEnumerable<Payment> payments) =>
        payments
            .GroupBy(p => p.Method)
            .Select(g => (Method: g.Key, Amount: g.Sum(p => p.Amount)))
            .Where(l => l.Amount != 0)
            .OrderBy(l => l.Method)
            .Select(l => (l.Method.Label(), l.Amount))
            .ToList();

    public static string? WaiterNames(IEnumerable<string?> userIds, IReadOnlyDictionary<string, string?> fullNames)
    {
        var names = userIds
            .Where(id => id != null && fullNames.TryGetValue(id, out var n) && !string.IsNullOrWhiteSpace(n))
            .Select(id => fullNames[id!]!.Trim())
            .Distinct()
            .ToList();
        return names.Count > 0 ? string.Join(", ", names) : null;
    }
}
