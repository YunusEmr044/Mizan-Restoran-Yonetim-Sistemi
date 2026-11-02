using RestoranYonetim.Models;

namespace RestoranYonetim.Services;

public sealed record SplitLine(int OrderItemId, string Name, decimal UnitTotal, int Quantity, int PaidQuantity)
{
    public int Remaining => Quantity - PaidQuantity;
}

public sealed record SplitAmount(decimal Amount, string? Error);

// Ürün bazlı hesap bölme: hangi kalemin kaçının ödendiği ödemelerin kalem kayıtlarından çıkar; iptal edilen ödeme kalemleri yeniden açar.
public static class ItemSplit
{
    public static IReadOnlyList<SplitLine> Lines(IEnumerable<OrderItem> items, IEnumerable<Payment> payments)
    {
        var paymentList = payments.ToList();
        var reversed = paymentList.Where(p => p.ReversesPaymentId != null).Select(p => p.ReversesPaymentId!.Value).ToHashSet();
        var paid = paymentList.Where(p => p.ReversesPaymentId == null && !reversed.Contains(p.Id))
            .SelectMany(p => p.Items)
            .GroupBy(i => i.OrderItemId)
            .ToDictionary(g => g.Key, g => g.Sum(i => i.Quantity));
        return items.Where(i => i.Status != OrderStatus.IptalEdildi && i.Quantity > 0)
            .Select(i => new SplitLine(i.Id, i.MenuItemName, i.UnitPrice + i.OptionsUnitPrice, i.Quantity, Math.Min(i.Quantity, paid.GetValueOrDefault(i.Id))))
            .ToList();
    }

    // Tutar adisyon indirimine oranla hesaplanır; kalan bütün kalemler seçildiyse kuruş farkı kalmasın diye kalan tutarın tamamıdır.
    public static SplitAmount Amount(IReadOnlyList<SplitLine> lines, IReadOnlyDictionary<int, int> selection, decimal itemsTotal, decimal grandTotal, decimal remaining)
    {
        var chosen = selection.Where(s => s.Value != 0).ToList();
        if (chosen.Count == 0)
        {
            return new SplitAmount(0, "Ödenecek ürünleri seçin.");
        }
        var byId = lines.ToDictionary(l => l.OrderItemId);
        decimal subtotal = 0;
        foreach (var (itemId, quantity) in chosen)
        {
            if (!byId.TryGetValue(itemId, out var line) || quantity < 0 || quantity > line.Remaining)
            {
                return new SplitAmount(0, "Seçilen ürünlerden biri zaten ödenmiş ya da adet fazla; sayfayı yenileyin.");
            }
            subtotal += line.UnitTotal * quantity;
        }

        var coversAll = lines.All(l => l.Remaining == 0 || selection.GetValueOrDefault(l.OrderItemId) == l.Remaining);
        var ratio = itemsTotal > 0 ? grandTotal / itemsTotal : 0;
        var amount = coversAll ? remaining : Math.Min(remaining, Math.Round(subtotal * ratio, 2, MidpointRounding.AwayFromZero));
        return amount > 0 ? new SplitAmount(amount, null) : new SplitAmount(0, "Bu adisyonun ödenecek tutarı kalmadı.");
    }
}
