using Microsoft.EntityFrameworkCore;
using RestoranYonetim.Data;
using RestoranYonetim.Models;

namespace RestoranYonetim.Services.Printing;

// Sipariş ve iptal fişlerini şube + bölüm yazıcılarına dağıtır. Kayıt commit edildikten sonra çağrılır; hata siparişi etkilemez.
public class KitchenPrintService
{
    public const string FeatureKey = "kitchen-printer";

    private readonly ApplicationDbContext _context;
    private readonly KitchenPrintWorker _worker;
    private readonly BrowserPrintTracker _tracker;
    private readonly RealtimeNotifier _realtime;
    private readonly IPrinterTransport _transport;
    private readonly ILogger<KitchenPrintService> _logger;

    public KitchenPrintService(ApplicationDbContext context, KitchenPrintWorker worker, BrowserPrintTracker tracker, RealtimeNotifier realtime,
        IPrinterTransport transport, ILogger<KitchenPrintService> logger)
    {
        _logger = logger;
        _context = context;
        _worker = worker;
        _tracker = tracker;
        _realtime = realtime;
        _transport = transport;
    }

    public Task<bool> IsEnabledAsync() => _context.FeatureSettings.AnyAsync(f => f.Key == FeatureKey && f.IsEnabled);

    public Task DispatchOrderAsync(int orderId) => GuardAsync(() => DispatchOrderCoreAsync(orderId), $"sipariş #{orderId}");

    public Task DispatchVoidAsync(int orderItemId, string? reason, string? by) =>
        GuardAsync(() => DispatchVoidCoreAsync(orderItemId, reason, by), $"iptal kalem #{orderItemId}");

    private async Task GuardAsync(Func<Task> dispatch, string what)
    {
        try
        {
            await dispatch();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Mutfak fişi gönderilemedi ({What}).", what);
        }
    }

    private async Task DispatchOrderCoreAsync(int orderId)
    {
        if (!await IsEnabledAsync())
        {
            return;
        }

        var order = await _context.Orders.AsNoTracking()
            .Include(o => o.Items).ThenInclude(i => i.MenuItem)
            .Include(o => o.Items).ThenInclude(i => i.Options)
            .Include(o => o.Table).ThenInclude(t => t!.Area)
            .Include(o => o.Adisyon)
            .FirstOrDefaultAsync(o => o.Id == orderId);
        if (order == null)
        {
            return;
        }

        var printers = await PrintersForAsync(order);
        foreach (var department in Enum.GetValues<MenuDepartment>())
        {
            if (KitchenTicket.ForOrder(order, department) is { } ticket)
            {
                await SendAsync(printers(department), ticket);
            }
        }
    }

    private async Task DispatchVoidCoreAsync(int orderItemId, string? reason, string? by)
    {
        if (!await IsEnabledAsync())
        {
            return;
        }

        var item = await _context.OrderItems.AsNoTracking()
            .Include(i => i.MenuItem)
            .Include(i => i.Options)
            .Include(i => i.Order).ThenInclude(o => o!.Table).ThenInclude(t => t!.Area)
            .Include(i => i.Order).ThenInclude(o => o!.Adisyon)
            .FirstOrDefaultAsync(i => i.Id == orderItemId);
        if (item?.Order == null || item.MenuItem == null)
        {
            return;
        }

        var printers = await PrintersForAsync(item.Order);
        await SendAsync(printers(item.MenuItem.Department), KitchenTicket.ForVoid(item, item.Quantity, reason, by));
    }

    // Panelden test: ağ yazıcısında sonuç beklenir ve hata metni döner; tarayıcı yazıcısında fiş istasyona gönderilir.
    public async Task<string?> SendTestAsync(KitchenPrinter printer)
    {
        var ticket = KitchenTicket.ForTest(printer.Name, DateTime.Now);
        if (printer.Connection == PrinterConnection.Ag)
        {
            var failure = await KitchenPrintWorker.SendAsync(_transport, NetworkJob(printer, ticket), DateTime.Now, CancellationToken.None);
            return failure?.Error;
        }
        await SendToStationAsync(printer, ticket);
        return null;
    }

    // Masa siparişinin şubesi masanın alanından, paket siparişininki adisyondan gelir.
    private async Task<Func<MenuDepartment, IReadOnlyList<KitchenPrinter>>> PrintersForAsync(Order order)
    {
        if ((order.Table?.Area?.BranchId ?? order.Adisyon?.BranchId) is not int branchId)
        {
            return _ => Array.Empty<KitchenPrinter>();
        }
        var printers = await _context.KitchenPrinters.AsNoTracking().Where(p => p.BranchId == branchId).ToListAsync();
        return department => KitchenPrintRouting.Select(printers, branchId, department);
    }

    private async Task SendAsync(IEnumerable<KitchenPrinter> printers, KitchenTicket ticket)
    {
        foreach (var printer in printers)
        {
            if (printer.Connection == PrinterConnection.Ag)
            {
                await _worker.EnqueueAsync(NetworkJob(printer, ticket));
            }
            else
            {
                await SendToStationAsync(printer, ticket);
            }
        }
    }

    private Task SendToStationAsync(KitchenPrinter printer, KitchenTicket ticket)
    {
        var token = _tracker.Register(printer, ticket, DateTime.Now);
        return _realtime.NotifyAsync(StationGroup(printer.Id), "kitchen-ticket", token.ToString());
    }

    public static string StationGroup(int printerId) => $"printer-{printerId}";

    private static NetworkPrintJob NetworkJob(KitchenPrinter printer, KitchenTicket ticket) =>
        new(printer.Id, printer.Name, printer.Host!, printer.Port, EscPosEncoder.Encode(ticket, printer.PaperWidthMm), ticket.Title, printer.Department);
}
