using RestoranYonetim.Models;

namespace RestoranYonetim.Services.Fiscal;

// Mali fiş durum geçişleri. Belirsiz sonuç (fiş basılmış olabilir) kasiyer cihazı kontrol etmeden tekrar denenmez.
public static class FiscalReceiptRules
{
    public const int ReceiptNoMax = 50;
    public const int ZNoMax = 20;

    public static readonly FiscalReceiptStatus[] OpenStatuses =
    {
        FiscalReceiptStatus.Bekliyor, FiscalReceiptStatus.Hata, FiscalReceiptStatus.Belirsiz, FiscalReceiptStatus.ElleGirilecek
    };

    public static bool IsClosed(FiscalReceiptStatus status) => !OpenStatuses.Contains(status);

    public static string? CanRetry(FiscalReceiptStatus status, bool confirmedNotPrinted) => status switch
    {
        _ when IsClosed(status) => "Bu mali fiş zaten kapandı.",
        FiscalReceiptStatus.ElleGirilecek => "Elle modda fiş otomatik kesilmez; cihazdaki fiş numarasını girin.",
        FiscalReceiptStatus.Belirsiz when !confirmedNotPrinted => "Önce cihazda bu adisyonun fişinin çıkmadığını kontrol edip onaylayın; çıktıysa fiş numarasını girin.",
        _ => null
    };

    public static string? CanEnterNumber(FiscalReceiptStatus status, string? receiptNo)
    {
        if (IsClosed(status))
        {
            return "Bu mali fiş zaten kapandı.";
        }
        var value = receiptNo?.Trim() ?? string.Empty;
        return value.Length == 0 || value.Length > ReceiptNoMax ? $"Fiş numarası zorunlu ve en fazla {ReceiptNoMax} karakter olabilir." : null;
    }

    public static string? CanCloseWithoutReceipt(FiscalReceiptStatus status, string? reason) =>
        IsClosed(status) ? "Bu mali fiş zaten kapandı."
        : string.IsNullOrWhiteSpace(reason) ? "Fişsiz kapatmak için gerekçe yazın."
        : null;

    public static bool ShouldCreateReceipt(bool moduleEnabled, FiscalDevice? device) => moduleEnabled && device?.IsActive == true;
}
