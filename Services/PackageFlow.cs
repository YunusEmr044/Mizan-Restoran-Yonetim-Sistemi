using RestoranYonetim.Models;

namespace RestoranYonetim.Services;

// Paket adisyonlarının teslim adımları. Modül kapalı olsa da mevcut paketler bu adımlarla kapatılabilir.
public static class PackageFlow
{
    public static string? HandOverBlockReason(Adisyon a)
    {
        if (a.OrderType != OrderType.GelAl)
        {
            return "Bu işlem yalnızca gel-al paketleri içindir.";
        }
        return a.Package?.HandedOverAt != null ? "Paket zaten teslim alındı." : Common(a);
    }

    public static string? DispatchBlockReason(Adisyon a, string? courierName)
    {
        if (a.OrderType != OrderType.AdreseTeslim)
        {
            return "Bu işlem yalnızca adrese teslim paketleri içindir.";
        }
        if (string.IsNullOrWhiteSpace(courierName))
        {
            return "Kurye adını girin.";
        }
        if (courierName.Trim().Length > PackageInfo.CourierMax)
        {
            return $"Kurye adı en fazla {PackageInfo.CourierMax} karakter olabilir.";
        }
        return a.Package?.DispatchedAt != null ? "Paket zaten yola çıktı." : Common(a);
    }

    public static string? DeliverBlockReason(Adisyon a)
    {
        if (a.OrderType != OrderType.AdreseTeslim)
        {
            return "Bu işlem yalnızca adrese teslim paketleri içindir.";
        }
        if (a.Package?.DispatchedAt == null)
        {
            return "Önce paketi yola çıkarın.";
        }
        return a.Package.DeliveredAt != null ? "Paket zaten teslim edildi." : Common(a);
    }

    // Taslağı silinmiş, mutfağa hiçbir şey gitmemiş paket iptal edilebilir.
    public static string? CancelBlockReason(Adisyon a)
    {
        if (a.OrderType == OrderType.Masa)
        {
            return "Bu işlem yalnızca paketler içindir.";
        }
        if (a.Payments.Count > 0)
        {
            return "Ödeme alınmış paket iptal edilemez.";
        }
        return a.Orders.Any(o => !o.IsDraft) ? "Mutfağa gönderilmiş paket buradan iptal edilemez; iptal talebi açın." : Common(a);
    }

    public static string Stage(Adisyon a)
    {
        if (a.Package?.DeliveredAt != null) return "Teslim edildi";
        if (a.Package?.DispatchedAt != null) return "Yolda";
        if (a.Package?.HandedOverAt != null) return "Teslim alındı";

        var items = a.Orders.Where(o => !o.IsDraft).SelectMany(o => o.Items).Where(i => i.Status != OrderStatus.IptalEdildi).ToList();
        if (items.Count == 0) return "Ürün bekleniyor";
        return items.All(i => i.Status is OrderStatus.Hazir or OrderStatus.ServisEdildi) ? "Hazır" : "Hazırlanıyor";
    }

    public static bool CanDispatch(Adisyon a) =>
        a.OrderType == OrderType.AdreseTeslim && a.Package?.DispatchedAt == null && Common(a) == null;

    // Ödenmiş paket hâlâ teslim edilebilir; iptal, iade ya da birleştirilmiş paket edilemez.
    private static string? Common(Adisyon a) =>
        a.Status is AdisyonStatus.Iptal or AdisyonStatus.Iade or AdisyonStatus.Birlestirildi ? "Bu paket kapalı." : null;
}
