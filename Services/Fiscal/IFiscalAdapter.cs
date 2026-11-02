namespace RestoranYonetim.Services.Fiscal;

public enum FiscalStatus { Basarili, Hata, Belirsiz }

public enum FiscalFieldKind { Text, Number, Secret, Select }

public sealed record FiscalCapabilities(bool AutomaticReceipt, bool CardPayment, bool VoidReceipt);

public sealed record FiscalSettingField(string Key, string Label, FiscalFieldKind Kind, bool Required = false, IReadOnlyList<(string Value, string Label)>? Options = null, string? Help = null);

// Belirsiz: cihaz yanıt vermedi ya da bağlantı işlem ortasında koptu; fiş basılmış olabilir, otomatik tekrar edilmez.
public sealed record FiscalResult(FiscalStatus Status, string? ReceiptNo = null, string? ZNo = null, string? Message = null);

public sealed record FiscalCardResult(bool Approved, string? AuthCode = null, string? Masked = null, string? Message = null);

public sealed record FiscalDeviceContext(int DeviceId, int BranchId, IReadOnlyDictionary<string, string> Settings)
{
    public string Setting(string key, string fallback = "") => Settings.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : fallback;
}

// Yazar kasa (ÖKC) bağdaştırıcısı. Yeni marka eklemek için bu arayüzü uygulayan bir sınıf yazılıp DI'a kaydedilir (bkz. README).
public interface IFiscalAdapter
{
    string Key { get; }

    string DisplayName { get; }

    FiscalCapabilities Capabilities { get; }

    IReadOnlyList<FiscalSettingField> SettingFields { get; }

    Task<FiscalResult> TestAsync(FiscalDeviceContext device, CancellationToken ct);

    Task<FiscalResult> PrintReceiptAsync(FiscalDeviceContext device, FiscalReceiptRequest receipt, CancellationToken ct);

    Task<FiscalCardResult> ChargeCardAsync(FiscalDeviceContext device, decimal amount, string reference, CancellationToken ct);

    Task<FiscalResult> VoidReceiptAsync(FiscalDeviceContext device, string receiptNo, CancellationToken ct);
}

public sealed class FiscalAdapterRegistry
{
    private readonly IReadOnlyDictionary<string, IFiscalAdapter> _adapters;

    public FiscalAdapterRegistry(IEnumerable<IFiscalAdapter> adapters)
    {
        var list = adapters.ToList();
        var duplicate = list.GroupBy(a => a.Key).FirstOrDefault(g => g.Count() > 1);
        if (duplicate != null)
        {
            throw new InvalidOperationException($"Aynı anahtarla iki yazar kasa bağdaştırıcısı kayıtlı: {duplicate.Key}");
        }
        _adapters = list.ToDictionary(a => a.Key);
    }

    public IReadOnlyList<IFiscalAdapter> All => _adapters.Values.OrderBy(a => a.DisplayName).ToList();

    public IFiscalAdapter? Get(string? key) => key != null && _adapters.TryGetValue(key, out var adapter) ? adapter : null;
}
