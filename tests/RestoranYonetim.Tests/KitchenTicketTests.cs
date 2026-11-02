using RestoranYonetim.Models;
using RestoranYonetim.Services.Printing;

namespace RestoranYonetim.Tests;

public class KitchenTicketTests
{
    private static OrderItem Item(string name, MenuDepartment dept, int qty = 1, OrderStatus status = OrderStatus.Yeni) =>
        new() { MenuItemName = name, Quantity = qty, Status = status, MenuItem = new MenuItem { Name = name, Department = dept } };

    private static Order OrderWith(params OrderItem[] items) => new()
    {
        Id = 42,
        IsDraft = false,
        SentAt = new DateTime(2026, 11, 4, 14, 32, 0),
        CreatedByUserName = "Garson",
        Source = OrderSource.Garson,
        Table = new RestaurantTable { Name = "Masa 5" },
        Items = items.ToList()
    };

    [Fact]
    public void ForOrder_Takes_Only_Department_Items()
    {
        var ticket = KitchenTicket.ForOrder(OrderWith(Item("Adana", MenuDepartment.Mutfak), Item("Ayran", MenuDepartment.Bar)), MenuDepartment.Mutfak);

        Assert.Equal("Adana", Assert.Single(ticket!.Lines).Name);
        Assert.Equal("Masa 5", ticket.Title);
        Assert.Equal(42, ticket.OrderId);
    }

    [Fact]
    public void ForOrder_Skips_Cancelled_Items()
    {
        var ticket = KitchenTicket.ForOrder(OrderWith(Item("Adana", MenuDepartment.Mutfak), Item("Urfa", MenuDepartment.Mutfak, status: OrderStatus.IptalEdildi)), MenuDepartment.Mutfak);

        Assert.Single(ticket!.Lines);
    }

    [Fact]
    public void ForOrder_Returns_Null_When_Department_Empty()
    {
        Assert.Null(KitchenTicket.ForOrder(OrderWith(Item("Ayran", MenuDepartment.Bar)), MenuDepartment.Mutfak));
    }

    [Fact]
    public void ForOrder_Carries_Options_Note_And_Takeaway()
    {
        var item = Item("Adana", MenuDepartment.Mutfak, 2);
        item.Notes = "Acısız";
        item.IsTakeaway = true;
        item.Options.Add(new OrderItemOption { Name = "Lavaş" });

        var line = Assert.Single(KitchenTicket.ForOrder(OrderWith(item), MenuDepartment.Mutfak)!.Lines);

        Assert.Equal(2, line.Quantity);
        Assert.Equal(new[] { "Lavaş" }, line.Options);
        Assert.Equal("Acısız", line.Note);
        Assert.True(line.Takeaway);
    }

    [Fact]
    public void ForOrder_Title_Uses_AdisyonLabel()
    {
        var order = OrderWith(Item("Adana", MenuDepartment.Mutfak));
        order.Table = null;
        order.Adisyon = new Adisyon { Id = 12, OrderType = OrderType.GelAl, Package = new PackageInfo { CustomerName = "Ahmet", PromisedAt = new DateTime(2026, 11, 4, 15, 0, 0) } };

        var ticket = KitchenTicket.ForOrder(order, MenuDepartment.Mutfak)!;

        Assert.Equal("GEL-AL #12 · Ahmet", ticket.Title);
        Assert.Equal(new DateTime(2026, 11, 4, 15, 0, 0), ticket.PromisedAt);
    }

    [Fact]
    public void ForVoid_Has_Reason_And_Quantity()
    {
        var item = Item("Adana", MenuDepartment.Mutfak, 3);
        item.Order = OrderWith(item);

        var ticket = KitchenTicket.ForVoid(item, 2, "Müşteri vazgeçti", "Müdür");

        Assert.Equal(KitchenTicketKind.Iptal, ticket.Kind);
        Assert.Equal(2, Assert.Single(ticket.Lines).Quantity);
        Assert.Equal("Müşteri vazgeçti", ticket.Reason);
        Assert.Equal("Masa 5", ticket.Title);
    }

    [Fact]
    public void Wrap_Breaks_On_Width()
    {
        var lines = EscPosEncoder.Wrap(new string('a', 50), 48);

        Assert.Equal(2, lines.Count);
        Assert.Equal(48, lines[0].Length);
    }

    [Fact]
    public void Wrap_Prefers_Word_Boundaries()
    {
        Assert.Equal(new[] { "Kaşarlı", "Köfte" }, EscPosEncoder.Wrap("Kaşarlı Köfte", 10));
    }

    private static KitchenTicket Turkish() => new(KitchenTicketKind.Siparis, "Masa İç", 1, new DateTime(2026, 11, 4, 12, 0, 0), null, null, null,
        new[] { new KitchenTicketLine(1, "ışğŞĞ", Array.Empty<string>(), null, false) }, null, null);

    [Fact]
    public void Encode_Uses_Cp857_For_Turkish()
    {
        var bytes = EscPosEncoder.Encode(Turkish(), 80);

        foreach (var b in new byte[] { 0x8D, 0x98, 0x9F, 0xA7, 0x9E, 0xA6 })
        {
            Assert.Contains(b, bytes);
        }
    }

    [Fact]
    public void Encode_Starts_With_Init_And_Codepage_Ends_With_Cut()
    {
        var bytes = EscPosEncoder.Encode(Turkish(), 80);

        Assert.Equal(new byte[] { 0x1B, 0x40, 0x1B, 0x74, 0x0D }, bytes.Take(5));
        Assert.Contains(Convert.ToHexString(new byte[] { 0x1D, 0x56 }), Convert.ToHexString(bytes));
    }

    [Fact]
    public void Encode_58mm_Wraps_At_32()
    {
        var ticket = Turkish() with { Lines = new[] { new KitchenTicketLine(1, new string('x', 40), Array.Empty<string>(), null, false) } };

        Assert.Equal(32, EscPosEncoder.LineWidth(58));
        Assert.Equal(48, EscPosEncoder.LineWidth(80));
        Assert.Contains(new string('x', 32 - "1 x ".Length), System.Text.Encoding.ASCII.GetString(EscPosEncoder.Encode(ticket, 58)));
    }

    [Fact]
    public void Void_Ticket_Shows_Approver_Once()
    {
        var item = Item("Adana", MenuDepartment.Mutfak);
        item.Order = OrderWith(item);
        var text = System.Text.Encoding.Latin1.GetString(EscPosEncoder.Encode(KitchenTicket.ForVoid(item, 1, null, "Mudur Ali"), 80));

        Assert.Single(System.Text.RegularExpressions.Regex.Matches(text, "Mudur Ali"));
    }
}
