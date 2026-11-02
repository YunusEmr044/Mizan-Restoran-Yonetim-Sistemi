using RestoranYonetim.Models;

namespace RestoranYonetim.Services;

// İptal/iade kuralları: geçerliyse null, değilse kullanıcıya gösterilecek Türkçe neden. Talep açılırken ve onay anında (güncel veriyle) çalıştırılır.
public static class VoidRules
{
    public static bool CanCancelItemDirectly(OrderItem item) => item.Status == OrderStatus.Yeni;

    public static string? CancelItemBlockReason(Adisyon adisyon, OrderItem item)
    {
        if (adisyon.IsClosed)
        {
            return "Bu hesap kapatılmış - kalem iptal edilemez.";
        }
        if (item.Status == OrderStatus.IptalEdildi)
        {
            return "Bu kalem zaten iptal edilmiş.";
        }

        var totalAfter = Math.Max(0, adisyon.ItemsTotal - item.LineTotal - adisyon.DiscountAmount);
        return totalAfter < adisyon.PaidTotal
            ? "Bu kalem iptal edilirse alınan ödeme hesabı aşar; önce ödeme iptali veya iade yapın."
            : null;
    }

    public static decimal MaxRefund(Adisyon adisyon) => Math.Max(0, adisyon.PaidTotal);

    public static string? RefundBlockReason(Adisyon adisyon, decimal amount)
    {
        if (adisyon.Status != AdisyonStatus.Odendi)
        {
            return "İade yalnızca ödemesi onaylanıp kapatılmış hesaplara yapılabilir.";
        }
        if (amount <= 0)
        {
            return "İade tutarı sıfırdan büyük olmalı.";
        }
        return amount > MaxRefund(adisyon)
            ? $"İade tutarı hesaba ödenen tutarı ({MaxRefund(adisyon):0.00} TL) aşamaz."
            : null;
    }

    public static string? PaymentVoidBlockReason(Adisyon adisyon, Payment payment)
    {
        if (payment.AdisyonId != adisyon.Id || adisyon.Payments.All(p => p.Id != payment.Id))
        {
            return "Ödeme bu hesaba ait değil.";
        }
        if (adisyon.IsClosed)
        {
            return "Hesap kapatılmış - ödeme iptali yerine iade yapın.";
        }
        if (payment.ReversesPaymentId != null)
        {
            return "Bu kayıt zaten bir iptal/iade kaydı.";
        }
        return adisyon.Payments.Any(p => p.ReversesPaymentId == payment.Id)
            ? "Bu ödeme zaten iptal edilmiş."
            : null;
    }

    public static string? AdisyonCancelBlockReason(Adisyon adisyon)
    {
        if (adisyon.IsClosed)
        {
            return "Bu hesap zaten kapatılmış.";
        }
        return adisyon.PaidTotal != 0
            ? "Ödeme alınmış bir hesap iptal edilemez; önce ödemeleri iptal edin."
            : null;
    }

    public static AdisyonStatus StatusAfterPaymentVoid(Adisyon adisyon) =>
        adisyon.PaidTotal > 0 && adisyon.PaidTotal < adisyon.GrandTotal ? AdisyonStatus.KismiOdendi : AdisyonStatus.Kasada;

    public static AdisyonStatus StatusAfterRefund(Adisyon adisyon) =>
        adisyon.PaidTotal <= 0 ? AdisyonStatus.Iade : AdisyonStatus.Odendi;
}
