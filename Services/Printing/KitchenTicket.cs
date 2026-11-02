using RestoranYonetim.Extensions;
using RestoranYonetim.Models;

namespace RestoranYonetim.Services.Printing;

public enum KitchenTicketKind { Siparis, Iptal, Test }

public sealed record KitchenTicketLine(int Quantity, string Name, IReadOnlyList<string> Options, string? Note, bool Takeaway);

// Mutfak/bar fişinin içeriği; ESC/POS ve HTML çıktıları bu modelden üretilir.
public sealed record KitchenTicket(
    KitchenTicketKind Kind,
    string Title,
    int OrderId,
    DateTime At,
    string? By,
    string? Source,
    DateTime? PromisedAt,
    IReadOnlyList<KitchenTicketLine> Lines,
    string? OrderNote,
    string? Reason)
{
    // İptal fişinde kişi "Onaylayan" satırında yazar; üst satır yalnızca sipariş fişinde.
    public string? Byline => Kind == KitchenTicketKind.Iptal
        ? null
        : string.Join(" · ", new[] { By, Source }.Where(s => !string.IsNullOrWhiteSpace(s))) is { Length: > 0 } line ? line : null;

    public static KitchenTicket? ForOrder(Order order, MenuDepartment department)
    {
        var lines = order.Items
            .Where(i => i.Status != OrderStatus.IptalEdildi && i.MenuItem?.Department == department)
            .Select(Line)
            .ToList();
        if (lines.Count == 0)
        {
            return null;
        }
        return new KitchenTicket(KitchenTicketKind.Siparis, AdisyonLabel.For(order), order.Id, order.SentAt ?? DateTime.Now,
            order.CreatedByUserName, order.Source.Label(), order.Adisyon?.Package?.PromisedAt, lines, Blank(order.Notes), null);
    }

    public static KitchenTicket ForVoid(OrderItem item, int quantity, string? reason, string? by) =>
        new(KitchenTicketKind.Iptal, item.Order == null ? "Masa" : AdisyonLabel.For(item.Order), item.OrderId, DateTime.Now, by, null,
            null, new[] { Line(item) with { Quantity = quantity } }, null, Blank(reason));

    public static KitchenTicket ForTest(string printerName, DateTime now) =>
        new(KitchenTicketKind.Test, "TEST FİŞİ", 0, now, null, null, null,
            new[] { new KitchenTicketLine(1, printerName, new[] { "Türkçe: ı İ ş Ş ğ Ğ ü Ü ö Ö ç Ç" }, null, false) }, null, null);

    private static KitchenTicketLine Line(OrderItem item) =>
        new(item.Quantity, item.MenuItemName, item.Options.Select(o => o.Name).ToList(), Blank(item.Notes), item.IsTakeaway);

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
