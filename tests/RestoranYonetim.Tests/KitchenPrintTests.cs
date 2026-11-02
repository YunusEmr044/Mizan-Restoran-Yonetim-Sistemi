using System.Net.Sockets;
using RestoranYonetim.Models;
using RestoranYonetim.Services.Printing;

namespace RestoranYonetim.Tests;

public class KitchenPrintTests
{
    private static readonly DateTime Now = new(2026, 11, 4, 14, 0, 0);

    private static KitchenPrinter Printer(int id, int branch, MenuDepartment dept, bool active = true) =>
        new() { Id = id, Name = $"P{id}", BranchId = branch, Department = dept, IsActive = active, Connection = PrinterConnection.Tarayici };

    private static KitchenTicket Ticket() => KitchenTicket.ForTest("P1", Now);

    [Fact]
    public void Routing_Picks_Active_Printers_Of_Branch_And_Department()
    {
        var printers = new[]
        {
            Printer(1, 1, MenuDepartment.Mutfak), Printer(2, 1, MenuDepartment.Mutfak),
            Printer(3, 1, MenuDepartment.Mutfak, active: false), Printer(4, 2, MenuDepartment.Mutfak), Printer(5, 1, MenuDepartment.Bar)
        };

        Assert.Equal(new[] { 1, 2 }, KitchenPrintRouting.Select(printers, 1, MenuDepartment.Mutfak).Select(p => p.Id));
    }

    [Fact]
    public void Tracker_Acknowledged_Ticket_Does_Not_Expire()
    {
        var tracker = new BrowserPrintTracker();
        var token = tracker.Register(Printer(1, 1, MenuDepartment.Mutfak), Ticket(), Now);

        Assert.True(tracker.Acknowledge(token));
        Assert.Empty(tracker.TakeExpired(Now.AddSeconds(30)));
        Assert.False(tracker.Acknowledge(token));
    }

    [Fact]
    public void Tracker_Expires_After_15_Seconds()
    {
        var tracker = new BrowserPrintTracker();
        tracker.Register(Printer(1, 1, MenuDepartment.Mutfak), Ticket(), Now);

        Assert.Empty(tracker.TakeExpired(Now.AddSeconds(14)));
        Assert.Single(tracker.TakeExpired(Now.AddSeconds(16)));
        Assert.Empty(tracker.TakeExpired(Now.AddSeconds(17)));
    }

    [Fact]
    public void Tracker_Get_Unknown_Token_Returns_Null()
    {
        Assert.Null(new BrowserPrintTracker().Get(Guid.NewGuid()));
    }

    [Fact]
    public void Tracker_Get_Returns_Ticket_And_Paper()
    {
        var tracker = new BrowserPrintTracker();
        var printer = Printer(7, 1, MenuDepartment.Bar);
        printer.PaperWidthMm = 58;
        var pending = tracker.Get(tracker.Register(printer, Ticket(), Now));

        Assert.Equal(7, pending!.PrinterId);
        Assert.Equal(58, pending.PaperWidthMm);
        Assert.Equal(MenuDepartment.Bar, pending.Department);
    }

    [Fact]
    public void FailureLog_Keeps_Recent_Per_Department()
    {
        var log = new PrintFailureLog();
        log.Add(new PrintFailure(Guid.NewGuid(), MenuDepartment.Mutfak, "P1", "Masa 1", Now.AddHours(-3), "x"));
        log.Add(new PrintFailure(Guid.NewGuid(), MenuDepartment.Bar, "P2", "Masa 2", Now, "x"));
        for (var i = 0; i < 25; i++)
        {
            log.Add(new PrintFailure(Guid.NewGuid(), MenuDepartment.Mutfak, "P1", $"Masa {i}", Now.AddMinutes(-i), "x"));
        }

        var recent = log.Recent(MenuDepartment.Mutfak, Now);

        Assert.Equal(20, recent.Count);
        Assert.Equal("Masa 0", recent[0].Label);
        Assert.Single(log.Recent(MenuDepartment.Bar, Now));
    }

    private sealed class FakeTransport : IPrinterTransport
    {
        public Exception? Throw { get; init; }
        public (string Host, int Port, byte[] Data)? Sent { get; private set; }

        public Task SendAsync(string host, int port, byte[] data, CancellationToken ct)
        {
            if (Throw != null) throw Throw;
            Sent = (host, port, data);
            return Task.CompletedTask;
        }
    }

    private static NetworkPrintJob Job() => new(1, "Ocak", "192.168.1.50", 9100, new byte[] { 1, 2, 3 }, "Masa 5", MenuDepartment.Mutfak);

    [Fact]
    public async Task Worker_Sends_Bytes_To_Host()
    {
        var transport = new FakeTransport();

        var failure = await KitchenPrintWorker.SendAsync(transport, Job(), Now, CancellationToken.None);

        Assert.Null(failure);
        Assert.Equal(("192.168.1.50", 9100), (transport.Sent!.Value.Host, transport.Sent.Value.Port));
        Assert.Equal(new byte[] { 1, 2, 3 }, transport.Sent.Value.Data);
    }

    [Fact]
    public async Task Worker_Reports_Failure_When_Transport_Throws()
    {
        var failure = await KitchenPrintWorker.SendAsync(new FakeTransport { Throw = new SocketException() }, Job(), Now, CancellationToken.None);

        Assert.NotNull(failure);
        Assert.Equal("Masa 5", failure!.Label);
        Assert.Equal(MenuDepartment.Mutfak, failure.Department);
    }

    [Fact]
    public void Failure_Notification_Targets_Department_Screen()
    {
        var bar = new PrintFailure(Guid.NewGuid(), MenuDepartment.Bar, "Bar yazıcısı", "Masa 2", Now, "x");

        Assert.Equal("/Bar", PrintFailure.ScreenPath(bar.Department));
        Assert.Equal(RestoranYonetim.Security.Permissions.Orders.Bar, PrintFailure.ScreenPermission(bar.Department));
        Assert.Contains("Masa 2", bar.Message);
    }

    [Theory]
    [InlineData("printer-5", 5)]
    [InlineData("PRINTER-12", 12)]
    [InlineData("printer-x", null)]
    [InlineData("printer-", null)]
    [InlineData("printer--1", null)]
    [InlineData("mutfak", null)]
    public void Hub_Printer_Group_Parsing(string group, int? expected)
    {
        Assert.Equal(expected, RestoranYonetim.Hubs.OpsHub.PrinterGroupId(group));
    }
}
