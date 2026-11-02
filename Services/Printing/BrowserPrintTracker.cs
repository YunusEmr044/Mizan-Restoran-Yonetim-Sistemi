using System.Collections.Concurrent;
using RestoranYonetim.Models;

namespace RestoranYonetim.Services.Printing;

public sealed record PendingBrowserTicket(Guid Token, int PrinterId, string PrinterName, MenuDepartment Department, int PaperWidthMm, KitchenTicket Ticket, DateTime Deadline);

// Tarayıcı istasyonuna gönderilen fişler; süresinde "basıldı" onayı gelmeyen fiş basılamamış sayılır.
public class BrowserPrintTracker
{
    public static readonly TimeSpan AckTimeout = TimeSpan.FromSeconds(15);

    private readonly ConcurrentDictionary<Guid, PendingBrowserTicket> _pending = new();

    public Guid Register(KitchenPrinter printer, KitchenTicket ticket, DateTime now)
    {
        var token = Guid.NewGuid();
        _pending[token] = new PendingBrowserTicket(token, printer.Id, printer.Name, printer.Department, printer.PaperWidthMm, ticket, now + AckTimeout);
        return token;
    }

    public PendingBrowserTicket? Get(Guid token) => _pending.TryGetValue(token, out var pending) ? pending : null;

    public bool Acknowledge(Guid token) => _pending.TryRemove(token, out _);

    public IReadOnlyList<PendingBrowserTicket> TakeExpired(DateTime now)
    {
        var expired = new List<PendingBrowserTicket>();
        foreach (var pending in _pending.Values.Where(p => p.Deadline < now))
        {
            if (_pending.TryRemove(pending.Token, out var removed))
            {
                expired.Add(removed);
            }
        }
        return expired;
    }
}
