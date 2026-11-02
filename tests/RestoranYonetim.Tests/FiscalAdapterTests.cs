using Microsoft.AspNetCore.DataProtection;
using RestoranYonetim.Models;
using RestoranYonetim.Security;
using RestoranYonetim.Services.Fiscal;

namespace RestoranYonetim.Tests;

public class FiscalAdapterTests
{
    private static FiscalDeviceContext Device(string behavior, int branch = 1) =>
        new(1, branch, new Dictionary<string, string> { ["behavior"] = behavior, ["delaySeconds"] = "0" });

    private static FiscalReceiptRequest Receipt() =>
        new(5, new[] { new FiscalLine("Adana", 1, 100, 10) }, 0, new[] { new FiscalPayment(PaymentMethod.Nakit, 100) }, 100, "ADS-5");

    [Fact]
    public async Task Simulator_Behaviors()
    {
        var sim = new SimulatorFiscalAdapter();

        var first = await sim.PrintReceiptAsync(Device("ok", branch: 9), Receipt(), CancellationToken.None);
        var second = await sim.PrintReceiptAsync(Device("ok", branch: 9), Receipt(), CancellationToken.None);
        Assert.Equal(FiscalStatus.Basarili, first.Status);
        Assert.Equal(int.Parse(first.ReceiptNo!) + 1, int.Parse(second.ReceiptNo!));

        Assert.Equal(FiscalStatus.Hata, (await sim.PrintReceiptAsync(Device("error"), Receipt(), CancellationToken.None)).Status);
        Assert.Equal(FiscalStatus.Belirsiz, (await sim.PrintReceiptAsync(Device("unknown"), Receipt(), CancellationToken.None)).Status);

        var every3 = new List<FiscalStatus>();
        for (var i = 0; i < 3; i++)
        {
            every3.Add((await sim.PrintReceiptAsync(Device("every3", branch: 77), Receipt(), CancellationToken.None)).Status);
        }
        Assert.Equal(new[] { FiscalStatus.Basarili, FiscalStatus.Basarili, FiscalStatus.Hata }, every3);
    }

    [Fact]
    public async Task Simulator_Card_Declines_On_Error()
    {
        var sim = new SimulatorFiscalAdapter();

        Assert.True((await sim.ChargeCardAsync(Device("ok"), 100, "ADS-5", CancellationToken.None)).Approved);
        Assert.False((await sim.ChargeCardAsync(Device("error"), 100, "ADS-5", CancellationToken.None)).Approved);
    }

    [Fact]
    public void Manual_Has_No_Automatic_Capabilities()
    {
        var caps = new ManualFiscalAdapter().Capabilities;

        Assert.False(caps.AutomaticReceipt);
        Assert.False(caps.CardPayment);
        Assert.False(caps.VoidReceipt);
    }

    [Fact]
    public void Registry_Keys_Unique_And_Unknown_Null()
    {
        var registry = new FiscalAdapterRegistry(new IFiscalAdapter[] { new SimulatorFiscalAdapter(), new ManualFiscalAdapter() });

        Assert.Equal(new[] { "manual", "simulator" }, registry.All.Select(a => a.Key).OrderBy(k => k));
        Assert.Null(registry.Get("yok"));
        Assert.Throws<InvalidOperationException>(() => new FiscalAdapterRegistry(new IFiscalAdapter[] { new ManualFiscalAdapter(), new ManualFiscalAdapter() }));
    }

    private static readonly IReadOnlyList<FiscalSettingField> Fields = new[]
    {
        new FiscalSettingField("host", "IP", FiscalFieldKind.Text, Required: true),
        new FiscalSettingField("apiKey", "API anahtarı", FiscalFieldKind.Secret, Required: true)
    };

    private static FiscalSettingsProtector Protector() => new(new EphemeralDataProtectionProvider());

    [Fact]
    public void Secret_Not_Plain_In_Json()
    {
        var protector = Protector();
        var (json, error) = protector.Protect(Fields, new Dictionary<string, string?> { ["host"] = "192.168.1.9", ["apiKey"] = "gizli-123" }, null);

        Assert.Null(error);
        Assert.DoesNotContain("gizli-123", json);
        Assert.Equal("gizli-123", protector.Unprotect(json!)["apiKey"]);
    }

    [Fact]
    public void Blank_Secret_Keeps_Existing()
    {
        var protector = Protector();
        var (first, _) = protector.Protect(Fields, new Dictionary<string, string?> { ["host"] = "a", ["apiKey"] = "gizli" }, null);

        var (second, error) = protector.Protect(Fields, new Dictionary<string, string?> { ["host"] = "b", ["apiKey"] = "" }, first);

        Assert.Null(error);
        var values = protector.Unprotect(second!);
        Assert.Equal(("b", "gizli"), (values["host"], values["apiKey"]));
    }

    [Fact]
    public void Required_Field_Missing_Is_Error()
    {
        Assert.NotNull(Protector().Protect(Fields, new Dictionary<string, string?> { ["host"] = "" , ["apiKey"] = "x" }, null).Error);
    }

    [Fact]
    public void Form_Never_Renders_Secret()
    {
        var protector = Protector();
        var (json, _) = protector.Protect(Fields, new Dictionary<string, string?> { ["host"] = "10.0.0.5", ["apiKey"] = "gizli" }, null);

        var form = protector.FormValues(Fields, json);

        Assert.Equal("10.0.0.5", form["host"]);
        Assert.Equal("", form["apiKey"]);
    }

    [Fact]
    public void FeatureCatalog_Fiscal_Off_By_Default()
    {
        var feature = Assert.Single(FeatureCatalog.All, f => f.Key == "fiscal");
        Assert.Equal("fiscal.", feature.PermissionPrefix);
        Assert.False(feature.DefaultEnabled);
    }

    [Fact]
    public void Mudur_Can_Manage_Fiscal()
    {
        Assert.Contains(Permissions.Fiscal.Manage, RolePermissions.Defaults[RolePermissions.Mudur]);
    }
}
