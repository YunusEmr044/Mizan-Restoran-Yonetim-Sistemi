using System.Collections.Concurrent;

namespace RestoranYonetim.Services.Fiscal;

// Cihaz yokken uçtan uca deneme için. Kesilen fişler panelde "SİMÜLATÖR" etiketiyle görünür.
public sealed class SimulatorFiscalAdapter : IFiscalAdapter
{
    public const string AdapterKey = "simulator";

    private readonly ConcurrentDictionary<int, int> _receiptNumbers = new();
    private readonly ConcurrentDictionary<int, int> _calls = new();

    public string Key => AdapterKey;

    public string DisplayName => "Simülatör (test)";

    public FiscalCapabilities Capabilities { get; } = new(AutomaticReceipt: true, CardPayment: true, VoidReceipt: true);

    public IReadOnlyList<FiscalSettingField> SettingFields { get; } = new[]
    {
        new FiscalSettingField("behavior", "Davranış", FiscalFieldKind.Select, Required: true, Options: new[]
        {
            ("ok", "Hep başarılı"), ("error", "Hep hata"), ("unknown", "Belirsiz (yanıt yok)"), ("every3", "Her üçte bir hata")
        }),
        new FiscalSettingField("delaySeconds", "Gecikme (sn, 0–10)", FiscalFieldKind.Number, Help: "Cihaz yanıt süresini taklit eder.")
    };

    public async Task<FiscalResult> TestAsync(FiscalDeviceContext device, CancellationToken ct)
    {
        await DelayAsync(device, ct);
        return Behavior(device) == "error" ? new FiscalResult(FiscalStatus.Hata, Message: "Simülatör: bağlantı hatası") : new FiscalResult(FiscalStatus.Basarili, Message: "Simülatör hazır");
    }

    public async Task<FiscalResult> PrintReceiptAsync(FiscalDeviceContext device, FiscalReceiptRequest receipt, CancellationToken ct)
    {
        await DelayAsync(device, ct);
        switch (Behavior(device))
        {
            case "error":
                return new FiscalResult(FiscalStatus.Hata, Message: "Simülatör: kağıt bitti");
            case "unknown":
                return new FiscalResult(FiscalStatus.Belirsiz, Message: "Simülatör: cihaz yanıt vermedi");
            case "every3" when _calls.AddOrUpdate(device.BranchId, 1, (_, n) => n + 1) % 3 == 0:
                return new FiscalResult(FiscalStatus.Hata, Message: "Simülatör: üçüncü denemede hata");
        }
        var number = _receiptNumbers.AddOrUpdate(device.BranchId, 1, (_, n) => n + 1);
        return new FiscalResult(FiscalStatus.Basarili, number.ToString("D4"), "0001", "Simülatör fişi");
    }

    public async Task<FiscalCardResult> ChargeCardAsync(FiscalDeviceContext device, decimal amount, string reference, CancellationToken ct)
    {
        await DelayAsync(device, ct);
        return Behavior(device) switch
        {
            "error" => new FiscalCardResult(false, Message: "Simülatör: kart reddedildi"),
            "unknown" => new FiscalCardResult(false, Message: "Simülatör: cihaz yanıt vermedi"),
            _ => new FiscalCardResult(true, AuthCode: Random.Shared.Next(100000, 999999).ToString(), Masked: "4242")
        };
    }

    public async Task<FiscalResult> VoidReceiptAsync(FiscalDeviceContext device, string receiptNo, CancellationToken ct)
    {
        await DelayAsync(device, ct);
        return Behavior(device) == "error" ? new FiscalResult(FiscalStatus.Hata, Message: "Simülatör: iptal edilemedi") : new FiscalResult(FiscalStatus.Basarili, receiptNo);
    }

    private static string Behavior(FiscalDeviceContext device) => device.Setting("behavior", "ok");

    private static Task DelayAsync(FiscalDeviceContext device, CancellationToken ct) =>
        int.TryParse(device.Setting("delaySeconds", "0"), out var seconds) && seconds > 0
            ? Task.Delay(TimeSpan.FromSeconds(Math.Min(seconds, 10)), ct)
            : Task.CompletedTask;
}

// Entegrasyonu olmayan cihaz: kasiyer fişi cihazdan kendisi keser ve fiş numarasını sisteme girer.
public sealed class ManualFiscalAdapter : IFiscalAdapter
{
    public const string AdapterKey = "manual";

    public string Key => AdapterKey;

    public string DisplayName => "Elle (entegrasyon yok)";

    public FiscalCapabilities Capabilities { get; } = new(AutomaticReceipt: false, CardPayment: false, VoidReceipt: false);

    public IReadOnlyList<FiscalSettingField> SettingFields { get; } = Array.Empty<FiscalSettingField>();

    public Task<FiscalResult> TestAsync(FiscalDeviceContext device, CancellationToken ct) =>
        Task.FromResult(new FiscalResult(FiscalStatus.Basarili, Message: "Elle modda bağlantı yok; fiş numaraları kasada girilir."));

    public Task<FiscalResult> PrintReceiptAsync(FiscalDeviceContext device, FiscalReceiptRequest receipt, CancellationToken ct) =>
        Task.FromResult(new FiscalResult(FiscalStatus.Hata, Message: "Elle modda fiş otomatik kesilmez."));

    public Task<FiscalCardResult> ChargeCardAsync(FiscalDeviceContext device, decimal amount, string reference, CancellationToken ct) =>
        Task.FromResult(new FiscalCardResult(false, Message: "Elle modda kart tahsilatı yok."));

    public Task<FiscalResult> VoidReceiptAsync(FiscalDeviceContext device, string receiptNo, CancellationToken ct) =>
        Task.FromResult(new FiscalResult(FiscalStatus.Hata, Message: "Elle modda fiş iptali cihazdan yapılır."));
}
