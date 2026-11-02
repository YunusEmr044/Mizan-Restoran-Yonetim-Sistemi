using RestoranYonetim.Models;

namespace RestoranYonetim.Services;

// Masa taşıma, birleştirme ve ürün aktarma kuralları; servis bunları transaction ve kilit altında uygular.
public static class TableTransferRules
{
    public static string? MoveOrMergeBlockReason(Adisyon source, int targetTableId, Adisyon? targetActive, bool sourceHasPendingVoid)
    {
        var common = CommonBlockReason(source, targetTableId);
        if (common != null)
        {
            return common;
        }

        if (targetActive != null && targetActive.OrderType != OrderType.Masa)
        {
            return "Paket siparişlerle birleştirme yapılamaz.";
        }

        return targetActive != null && sourceHasPendingVoid
            ? "Önce bekleyen iptal/iade talebini sonuçlandırın, sonra birleştirin."
            : null;
    }

    public static string? ItemTransferBlockReason(Adisyon source, int targetTableId, IReadOnlyDictionary<int, int> quantities, bool sourceHasPendingVoid)
    {
        var common = CommonBlockReason(source, targetTableId);
        if (common != null)
        {
            return common;
        }

        if (quantities.Count == 0)
        {
            return "Aktarılacak ürünü seçin.";
        }
        var sourceBlock = ItemSourceBlockReason(source);
        if (sourceBlock != null)
        {
            return sourceBlock;
        }
        if (sourceHasPendingVoid)
        {
            return "Önce bekleyen iptal/iade talebini sonuçlandırın.";
        }

        var items = source.Orders.Where(o => !o.IsDraft).SelectMany(o => o.Items).ToDictionary(i => i.Id);
        foreach (var (itemId, quantity) in quantities)
        {
            if (!items.TryGetValue(itemId, out var item))
            {
                return "Seçilen ürün bu adisyonda bulunamadı.";
            }
            if (item.Status == OrderStatus.IptalEdildi)
            {
                return $"{item.MenuItemName} iptal edilmiş, aktarılamaz.";
            }
            if (quantity < 1 || quantity > item.Quantity)
            {
                return $"{item.MenuItemName} için 1 ile {item.Quantity} arasında adet seçin.";
            }
        }

        return null;
    }

    // Ürün aktarmayı adet ve hedeften bağımsız olarak engelleyen kaynak durumu (ekranda uyarı için de kullanılır).
    public static string? ItemSourceBlockReason(Adisyon source)
    {
        if (source.PaidTotal != 0)
        {
            return "Ödeme alınmış adisyondan ürün aktarılamaz.";
        }
        return source.DiscountAmount > 0 || source.DiscountRequests.Any(d => d.Status == DiscountRequestStatus.Beklemede)
            ? "İndirim uygulanmış ya da indirim talebi bekleyen adisyondan ürün aktarılamaz."
            : null;
    }

    public static OrderItem SplitOff(OrderItem item, int quantity)
    {
        if (quantity < 1 || quantity >= item.Quantity)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Bölme için kalemin adedinden küçük, pozitif bir adet gerekir.");
        }

        item.Quantity -= quantity;
        var moved = new OrderItem
        {
            MenuItemId = item.MenuItemId,
            MenuItemName = item.MenuItemName,
            UnitPrice = item.UnitPrice,
            VatRate = item.VatRate,
            Quantity = quantity,
            Notes = item.Notes,
            Status = item.Status,
            IsTakeaway = item.IsTakeaway
        };
        moved.Options.AddRange(item.Options.Select(o => new OrderItemOption { Name = o.Name, ExtraPrice = o.ExtraPrice }));
        return moved;
    }

    private static string? CommonBlockReason(Adisyon source, int targetTableId)
    {
        if (source.IsClosed)
        {
            return "Bu adisyon kapalı.";
        }
        if (source.OrderType != OrderType.Masa)
        {
            return "Paket siparişler masaya taşınamaz.";
        }
        return source.TableId == targetTableId ? "Hedef masa mevcut masayla aynı." : null;
    }
}
