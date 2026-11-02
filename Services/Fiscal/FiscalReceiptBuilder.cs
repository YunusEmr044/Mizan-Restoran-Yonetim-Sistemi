using RestoranYonetim.Models;

namespace RestoranYonetim.Services.Fiscal;

// Mali fiş KDV oranları; varsayılanlar başlangıç değeridir, işletme mali müşavirle teyit eder.
public static class VatRates
{
    public static readonly int[] Allowed = { 0, 1, 10, 20 };
    public const int Default = 10;

    public static bool IsValid(int rate) => Allowed.Contains(rate);

    public static int Resolve(int? itemRate, int categoryRate) => itemRate ?? categoryRate;

    // Ürünün kendi oranı, yoksa kategorisinin oranı (kategori yüklenmemişse varsayılan).
    public static int For(MenuItem item) => Resolve(item.VatRate, item.Category?.VatRate ?? Default);
}

public sealed record FiscalLine(string Name, int Quantity, decimal UnitPrice, int VatRate);

public sealed record FiscalPayment(PaymentMethod Method, decimal Amount);

public sealed record FiscalReceiptRequest(int AdisyonId, IReadOnlyList<FiscalLine> Lines, decimal Discount, IReadOnlyList<FiscalPayment> Payments, decimal Total, string Reference);

// Kapanan adisyondan mali fiş isteği: kalemler, indirim ve ödeme dağılımı; toplamlar tutmazsa fiş kesilmez.
public static class FiscalReceiptBuilder
{
    public const int NameMax = 32;

    public static (FiscalReceiptRequest? Request, string? Error) Build(Adisyon adisyon)
    {
        var lines = adisyon.Orders
            .Where(o => !o.IsDraft && o.Status != OrderStatus.IptalEdildi)
            .SelectMany(o => o.Items)
            .Where(i => i.Status != OrderStatus.IptalEdildi)
            .Select(i => new FiscalLine(Truncate(i.MenuItemName), i.Quantity, i.UnitPrice + i.OptionsUnitPrice, i.VatRate))
            .ToList();
        if (lines.Count == 0)
        {
            return (null, "Fişe yazılacak kalem yok.");
        }

        var linesTotal = lines.Sum(l => l.UnitPrice * l.Quantity);
        var total = adisyon.GrandTotal;
        var discount = linesTotal - total;

        // Ters kaydı olan ödeme ve onun ters kaydı fişe girmez; iade (eksi kayıt) kendi yönteminden düşülür.
        var reversed = adisyon.Payments.Where(p => p.ReversesPaymentId != null).Select(p => p.ReversesPaymentId).ToHashSet();
        var payments = adisyon.Payments
            .Where(p => p.ReversesPaymentId == null && !reversed.Contains(p.Id))
            .GroupBy(p => p.Method)
            .Select(g => new FiscalPayment(g.Key, g.Sum(p => p.Amount)))
            .Where(p => p.Amount > 0)
            .OrderBy(p => p.Method)
            .ToList();

        if (discount < 0 || payments.Sum(p => p.Amount) != total)
        {
            return (null, $"Fiş tutarları tutmuyor: kalemler {linesTotal:0.00}, indirim {discount:0.00}, ödemeler {payments.Sum(p => p.Amount):0.00}, toplam {total:0.00}.");
        }

        return (new FiscalReceiptRequest(adisyon.Id, lines, discount, payments, total, "ADS-" + adisyon.Id), null);
    }

    private static string Truncate(string name) => name.Length <= NameMax ? name : name[..NameMax];
}
