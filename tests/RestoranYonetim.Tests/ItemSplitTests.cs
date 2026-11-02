using RestoranYonetim.Models;
using RestoranYonetim.Services;

namespace RestoranYonetim.Tests;

public class ItemSplitTests
{
    private static OrderItem Item(int id, string name, decimal price, int qty, OrderStatus status = OrderStatus.Yeni, decimal option = 0)
    {
        var item = new OrderItem { Id = id, MenuItemName = name, UnitPrice = price, Quantity = qty, Status = status };
        if (option > 0)
        {
            item.Options.Add(new OrderItemOption { ExtraPrice = option });
        }
        return item;
    }

    private static readonly OrderItem[] Items =
    {
        Item(1, "Adana", 300, 2),
        Item(2, "Ayran", 40, 3),
        Item(3, "Künefe", 150, 1, option: 20),
        Item(4, "İptal", 99, 1, OrderStatus.IptalEdildi)
    };

    private static Payment ItemPayment(int id, params (int ItemId, int Qty)[] lines)
    {
        var payment = new Payment { Id = id, Amount = 1 };
        payment.Items.AddRange(lines.Select(l => new PaymentItem { OrderItemId = l.ItemId, Quantity = l.Qty }));
        return payment;
    }

    [Fact]
    public void Lines_Skip_Cancelled_Items_And_Count_Paid_Quantities()
    {
        var lines = ItemSplit.Lines(Items, new[] { ItemPayment(10, (2, 1)) });
        Assert.Equal(new[] { 1, 2, 3 }, lines.Select(l => l.OrderItemId));
        Assert.Equal(2, lines.Single(l => l.OrderItemId == 2).Remaining);
        Assert.Equal(170, lines.Single(l => l.OrderItemId == 3).UnitTotal);
    }

    [Fact]
    public void Reversed_Payment_Frees_Its_Items()
    {
        var reversal = new Payment { Id = 11, Amount = -40, ReversesPaymentId = 10 };
        var lines = ItemSplit.Lines(Items, new[] { ItemPayment(10, (2, 1)), reversal });
        Assert.Equal(3, lines.Single(l => l.OrderItemId == 2).Remaining);
    }

    [Fact]
    public void Amount_Applies_Adisyon_Discount_Proportionally()
    {
        var lines = ItemSplit.Lines(Items, Array.Empty<Payment>());
        // Kalem toplamı 890; 89 TL indirim → %10 indirim.
        var result = ItemSplit.Amount(lines, new Dictionary<int, int> { [1] = 1 }, itemsTotal: 890, grandTotal: 801, remaining: 801);
        Assert.Null(result.Error);
        Assert.Equal(270.00m, result.Amount);
    }

    [Fact]
    public void Selecting_Everything_Left_Pays_Exact_Remaining()
    {
        var lines = ItemSplit.Lines(Items, new[] { ItemPayment(10, (1, 1)) });
        var result = ItemSplit.Amount(lines, new Dictionary<int, int> { [1] = 1, [2] = 3, [3] = 1 }, itemsTotal: 890, grandTotal: 801, remaining: 530.99m);
        Assert.Equal(530.99m, result.Amount);
    }

    [Theory]
    [InlineData(2, 4)]
    [InlineData(99, 1)]
    [InlineData(1, 0)]
    public void Invalid_Selection_Is_Rejected(int itemId, int qty)
    {
        var lines = ItemSplit.Lines(Items, Array.Empty<Payment>());
        var result = ItemSplit.Amount(lines, new Dictionary<int, int> { [itemId] = qty }, 890, 890, 890);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void Amount_Never_Exceeds_Remaining()
    {
        var lines = ItemSplit.Lines(Items, Array.Empty<Payment>());
        var result = ItemSplit.Amount(lines, new Dictionary<int, int> { [1] = 2 }, 890, 890, remaining: 100);
        Assert.Equal(100, result.Amount);
    }
}
