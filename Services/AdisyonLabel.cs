using RestoranYonetim.Models;

namespace RestoranYonetim.Services;

// Mutfak, kasa, garson ekranları ve çıktılarda adisyonun görünen adı.
public static class AdisyonLabel
{
    public static string For(OrderType type, int adisyonId, string? tableName, string? customerName)
    {
        if (type == OrderType.Masa)
        {
            return string.IsNullOrWhiteSpace(tableName) ? "Masa" : tableName;
        }

        var prefix = type == OrderType.GelAl ? "GEL-AL" : "TESLİM";
        return string.IsNullOrWhiteSpace(customerName) ? $"{prefix} #{adisyonId}" : $"{prefix} #{adisyonId} · {customerName.Trim()}";
    }

    public static string For(Adisyon adisyon) =>
        For(adisyon.OrderType, adisyon.Id, adisyon.Table?.Name, adisyon.Package?.CustomerName);

    // Siparişin masası, adisyonun masası yüklenmemiş olsa da adı verir; taslak siparişin adisyonu olmayabilir.
    public static string For(Order order) =>
        order.Adisyon is { } a
            ? For(a.OrderType, a.Id, a.Table?.Name ?? order.Table?.Name, a.Package?.CustomerName)
            : For(OrderType.Masa, 0, order.Table?.Name, null);
}
