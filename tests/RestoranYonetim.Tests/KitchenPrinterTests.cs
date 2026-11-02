using RestoranYonetim.Models;
using RestoranYonetim.Security;
using RestoranYonetim.Services;

namespace RestoranYonetim.Tests;

public class KitchenPrinterTests
{
    private static KitchenPrinter Network(string? host, int port = 9100, int paper = 80) =>
        new() { Name = "Ocak", BranchId = 1, Connection = PrinterConnection.Ag, Host = host, Port = port, PaperWidthMm = paper };

    [Fact]
    public void Network_Requires_Host()
    {
        Assert.NotNull(KitchenPrinterValidation.Validate(Network(null)));
    }

    [Theory]
    [InlineData("localhost")]
    [InlineData("127.0.0.1")]
    [InlineData("127.5.5.5")]
    [InlineData("::1")]
    [InlineData("0.0.0.0")]
    public void Rejects_Loopback_Hosts(string host)
    {
        Assert.NotNull(KitchenPrinterValidation.Validate(Network(host)));
    }

    [Fact]
    public void Accepts_Lan_Address()
    {
        Assert.Null(KitchenPrinterValidation.Validate(Network("192.168.1.50")));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(70000)]
    public void Port_Out_Of_Range(int port)
    {
        Assert.NotNull(KitchenPrinterValidation.Validate(Network("192.168.1.50", port)));
    }

    [Fact]
    public void Paper_Must_Be_80_Or_58()
    {
        Assert.NotNull(KitchenPrinterValidation.Validate(Network("192.168.1.50", paper: 72)));
        Assert.Null(KitchenPrinterValidation.Validate(Network("192.168.1.50", paper: 58)));
    }

    [Fact]
    public void Browser_Printer_Needs_No_Host()
    {
        Assert.Null(KitchenPrinterValidation.Validate(new KitchenPrinter { Name = "Bar", BranchId = 1, Connection = PrinterConnection.Tarayici }));
    }

    [Fact]
    public void Name_Required()
    {
        var printer = Network("192.168.1.50");
        printer.Name = " ";
        Assert.NotNull(KitchenPrinterValidation.Validate(printer));
    }

    [Fact]
    public void FeatureCatalog_Has_KitchenPrinter_Off_By_Default()
    {
        var feature = Assert.Single(FeatureCatalog.All, f => f.Key == "kitchen-printer");
        Assert.Equal("printers.", feature.PermissionPrefix);
        Assert.False(feature.DefaultEnabled);
    }

    [Fact]
    public void Mudur_Can_Manage_Printers()
    {
        Assert.Contains(Permissions.Printers.Manage, RolePermissions.Defaults[RolePermissions.Mudur]);
    }
}
