using Microsoft.EntityFrameworkCore;
using RestoranYonetim.Data;
using RestoranYonetim.Extensions;
using RestoranYonetim.Models;
using RestoranYonetim.Security;

namespace RestoranYonetim.Services.Fiscal;

public sealed record FiscalOutcome(FiscalReceiptStatus Status, string? ReceiptNo, string? Message);

// Yazar kasa (ÖKC) akışı: kapanışta fiş kaydı, senkron yazdırma, tekrar/elle/fişsiz kapatma, kart tahsilatı ve iade.
public class FiscalService
{
    public const string FeatureKey = "fiscal";
    public static readonly TimeSpan ReceiptTimeout = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan CardTimeout = TimeSpan.FromSeconds(120);

    private readonly ApplicationDbContext _context;
    private readonly FiscalAdapterRegistry _adapters;
    private readonly FiscalSettingsProtector _settings;
    private readonly AuditService _audit;
    private readonly ILogger<FiscalService> _logger;

    public FiscalService(ApplicationDbContext context, FiscalAdapterRegistry adapters, FiscalSettingsProtector settings, AuditService audit, ILogger<FiscalService> logger)
    {
        _context = context;
        _adapters = adapters;
        _settings = settings;
        _audit = audit;
        _logger = logger;
    }

    public Task<bool> IsEnabledAsync() => _context.FeatureSettings.AnyAsync(f => f.Key == FeatureKey && f.IsEnabled);

    public Task<FiscalDevice?> DeviceForBranchAsync(int? branchId) =>
        branchId == null
            ? Task.FromResult<FiscalDevice?>(null)
            : _context.FiscalDevices.FirstOrDefaultAsync(d => d.BranchId == branchId && d.IsActive);

    public async Task<int?> BranchOfAsync(int adisyonId) =>
        await _context.Adisyonlar.AsNoTracking().Where(a => a.Id == adisyonId)
            .Select(a => a.TableId != null ? (int?)a.Table!.Area!.BranchId : a.BranchId)
            .FirstOrDefaultAsync();

    // Kapanışla aynı transaction'da kaydedilmek üzere fiş satırı ekler; SaveChanges çağırmaz.
    public async Task<FiscalReceipt?> CreateForClosingAsync(Adisyon adisyon)
    {
        var device = await DeviceForBranchAsync(await BranchOfAsync(adisyon.Id));
        if (!FiscalReceiptRules.ShouldCreateReceipt(await IsEnabledAsync(), device) || _adapters.Get(device!.AdapterKey) is not { } adapter)
        {
            return null;
        }
        var receipt = new FiscalReceipt
        {
            AdisyonId = adisyon.Id,
            FiscalDeviceId = device.Id,
            Total = adisyon.GrandTotal,
            Status = adapter.Capabilities.AutomaticReceipt ? FiscalReceiptStatus.Bekliyor : FiscalReceiptStatus.ElleGirilecek,
            IsSimulated = adapter.Key == SimulatorFiscalAdapter.AdapterKey
        };
        _context.FiscalReceipts.Add(receipt);
        return receipt;
    }

    public async Task<FiscalOutcome> TryPrintAsync(int receiptId, string? userName)
    {
        var receipt = await _context.FiscalReceipts.Include(r => r.FiscalDevice).FirstAsync(r => r.Id == receiptId);
        if (receipt.FiscalDevice == null || _adapters.Get(receipt.FiscalDevice.AdapterKey) is not { } adapter)
        {
            return await FailAsync(receipt, FiscalReceiptStatus.Hata, "Şubenin yazar kasa cihazı tanımlı değil.");
        }

        var adisyon = await _context.Adisyonlar.AsNoTracking().IncludeTotals().FirstAsync(a => a.Id == receipt.AdisyonId);
        var (request, error) = FiscalReceiptBuilder.Build(adisyon);
        if (request == null)
        {
            return await FailAsync(receipt, FiscalReceiptStatus.Hata, error);
        }

        FiscalResult result;
        using (var timeout = new CancellationTokenSource(ReceiptTimeout))
        {
            try
            {
                result = await adapter.PrintReceiptAsync(Context(receipt.FiscalDevice), request, timeout.Token);
            }
            catch (Exception ex)
            {
                // İstek cihaza ulaşmış olabilir; fiş basılmış olabileceği için sonuç belirsiz sayılır.
                _logger.LogWarning(ex, "Mali fiş gönderilemedi (adisyon {AdisyonId}).", receipt.AdisyonId);
                result = new FiscalResult(FiscalStatus.Belirsiz, Message: ex is OperationCanceledException ? "Cihaz zamanında yanıt vermedi." : "Cihazla bağlantı koptu.");
            }
        }

        receipt.Attempts++;
        if (result.Status == FiscalStatus.Basarili)
        {
            receipt.Status = FiscalReceiptStatus.Kesildi;
            receipt.ReceiptNo = Cut(result.ReceiptNo, FiscalReceiptRules.ReceiptNoMax);
            receipt.ZNo = Cut(result.ZNo, FiscalReceiptRules.ZNoMax);
            receipt.PrintedAt = DateTime.Now;
            receipt.PrintedByUserName = userName;
            receipt.LastError = null;
            await _context.SaveChangesAsync();
            await _audit.LogAsync(null, userName, "Mali fiş kesildi", entityName: "Adisyon", entityId: receipt.AdisyonId.ToString(),
                details: $"Fiş #{receipt.ReceiptNo}, Z #{receipt.ZNo}, {receipt.Total:0.00} TL{(receipt.IsSimulated ? " (simülatör)" : "")}");
            return new FiscalOutcome(receipt.Status, receipt.ReceiptNo, result.Message);
        }
        return await FailAsync(receipt, result.Status == FiscalStatus.Belirsiz ? FiscalReceiptStatus.Belirsiz : FiscalReceiptStatus.Hata, result.Message);
    }

    public async Task<string?> RetryAsync(int receiptId, bool confirmedNotPrinted, string? userName)
    {
        var receipt = await _context.FiscalReceipts.FirstOrDefaultAsync(r => r.Id == receiptId);
        if (receipt == null)
        {
            return "Mali fiş bulunamadı.";
        }
        if (FiscalReceiptRules.CanRetry(receipt.Status, confirmedNotPrinted) is string error)
        {
            return error;
        }
        if (receipt.Status == FiscalReceiptStatus.Belirsiz)
        {
            await _audit.LogAsync(null, userName, "Mali fiş belirsiz - cihazda çıkmadığı onaylandı", entityName: "Adisyon", entityId: receipt.AdisyonId.ToString());
        }
        var outcome = await TryPrintAsync(receiptId, userName);
        return outcome.Status == FiscalReceiptStatus.Kesildi ? null : outcome.Message ?? "Mali fiş kesilemedi.";
    }

    public async Task<string?> EnterNumberAsync(int receiptId, string? receiptNo, string? zNo, string? userName)
    {
        var receipt = await _context.FiscalReceipts.FirstOrDefaultAsync(r => r.Id == receiptId);
        if (receipt == null)
        {
            return "Mali fiş bulunamadı.";
        }
        if (FiscalReceiptRules.CanEnterNumber(receipt.Status, receiptNo) is string error)
        {
            return error;
        }
        receipt.Status = FiscalReceiptStatus.Kesildi;
        receipt.ReceiptNo = receiptNo!.Trim();
        receipt.ZNo = Cut(zNo?.Trim(), FiscalReceiptRules.ZNoMax);
        receipt.PrintedAt = DateTime.Now;
        receipt.PrintedByUserName = userName;
        receipt.LastError = null;
        await _context.SaveChangesAsync();
        await _audit.LogAsync(null, userName, "Mali fiş numarası elle girildi", entityName: "Adisyon", entityId: receipt.AdisyonId.ToString(),
            details: $"Fiş #{receipt.ReceiptNo}{(receipt.ZNo == null ? "" : $", Z #{receipt.ZNo}")}");
        return null;
    }

    public async Task<string?> CloseWithoutReceiptAsync(int receiptId, string? reason, string? userName)
    {
        var receipt = await _context.FiscalReceipts.FirstOrDefaultAsync(r => r.Id == receiptId);
        if (receipt == null)
        {
            return "Mali fiş bulunamadı.";
        }
        if (FiscalReceiptRules.CanCloseWithoutReceipt(receipt.Status, reason) is string error)
        {
            return error;
        }
        receipt.Status = FiscalReceiptStatus.FissizKapatildi;
        receipt.LastError = Cut("Fişsiz kapatıldı: " + reason!.Trim(), 500);
        await _context.SaveChangesAsync();
        await _audit.LogAsync(null, userName, "Mali fiş fişsiz kapatıldı", entityName: "Adisyon", entityId: receipt.AdisyonId.ToString(), details: reason.Trim());
        return null;
    }

    public async Task<(bool Enabled, bool SupportsCard)> CardOptionAsync(int? branchId)
    {
        if (!await IsEnabledAsync() || await DeviceForBranchAsync(branchId) is not { } device)
        {
            return (false, false);
        }
        return (true, _adapters.Get(device.AdapterKey)?.Capabilities.CardPayment == true);
    }

    // Cihaz yoksa ya da kart tahsilatını desteklemiyorsa null döner.
    public async Task<FiscalCardResult?> ChargeCardAsync(int? branchId, decimal amount, string reference)
    {
        if (!await IsEnabledAsync() || await DeviceForBranchAsync(branchId) is not { } device
            || _adapters.Get(device.AdapterKey) is not { Capabilities.CardPayment: true } adapter)
        {
            return null;
        }
        using var timeout = new CancellationTokenSource(CardTimeout);
        try
        {
            return await adapter.ChargeCardAsync(Context(device), amount, reference, timeout.Token);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Kart tahsilatı cihaza gönderilemedi ({Reference}).", reference);
            return new FiscalCardResult(false, Message: "Cihazdan yanıt alınamadı; cihazda işlem onaylandıysa \"POS'tan çek\" kapalıyken ödemeyi elle ekleyin.");
        }
    }

    // Kesilmiş fişi olan adisyon iade edilir ya da iptal edilirse: cihaz destekliyorsa fiş iptali, yoksa yöneticiye uyarı.
    public async Task<string?> OnRefundOrCancelAsync(int adisyonId, bool fullReversal, string? userName)
    {
        var receipt = await _context.FiscalReceipts.Include(r => r.FiscalDevice).FirstOrDefaultAsync(r => r.AdisyonId == adisyonId);
        if (receipt == null || receipt.Status != FiscalReceiptStatus.Kesildi)
        {
            return null;
        }

        var adapter = receipt.FiscalDevice == null ? null : _adapters.Get(receipt.FiscalDevice.AdapterKey);
        if (fullReversal && adapter?.Capabilities.VoidReceipt == true && receipt.ReceiptNo != null)
        {
            using var timeout = new CancellationTokenSource(ReceiptTimeout);
            try
            {
                var result = await adapter.VoidReceiptAsync(Context(receipt.FiscalDevice!), receipt.ReceiptNo, timeout.Token);
                if (result.Status == FiscalStatus.Basarili)
                {
                    receipt.Status = FiscalReceiptStatus.Iptal;
                    await _context.SaveChangesAsync();
                    await _audit.LogAsync(null, userName, "Mali fiş iptal edildi", entityName: "Adisyon", entityId: adisyonId.ToString(), details: $"Fiş #{receipt.ReceiptNo}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Mali fiş iptali gönderilemedi (adisyon {AdisyonId}).", adisyonId);
            }
        }

        var warning = $"Mali fiş #{receipt.ReceiptNo} için iade belgesini cihazdan ya da elle düzenleyin.";
        _context.Notifications.Add(new Notification { Message = warning, Link = "/Kasa", RequiredPermission = Permissions.Cash.ApproveRefund });
        await _context.SaveChangesAsync();
        return warning;
    }

    public Task<List<FiscalReceipt>> PendingAsync() =>
        _context.FiscalReceipts.AsNoTracking()
            .Include(r => r.Adisyon).ThenInclude(a => a!.Table)
            .Where(r => FiscalReceiptRules.OpenStatuses.Contains(r.Status))
            .OrderBy(r => r.CreatedAt)
            .ToListAsync();

    public FiscalDeviceContext Context(FiscalDevice device) => new(device.Id, device.BranchId, _settings.Unprotect(device.SettingsJson));

    private async Task<FiscalOutcome> FailAsync(FiscalReceipt receipt, FiscalReceiptStatus status, string? message)
    {
        receipt.Status = status;
        receipt.LastError = Cut(message, 500);
        await _context.SaveChangesAsync();
        await _audit.LogAsync(null, null, status == FiscalReceiptStatus.Belirsiz ? "Mali fiş sonucu belirsiz" : "Mali fiş kesilemedi",
            entityName: "Adisyon", entityId: receipt.AdisyonId.ToString(), details: message);
        return new FiscalOutcome(status, null, message);
    }

    private static string? Cut(string? value, int max) => value == null ? null : value.Length <= max ? value : value[..max];
}
