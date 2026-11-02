using RestoranYonetim.Models;
using RestoranYonetim.Services;

namespace RestoranYonetim.Tests;

public class TableTransferRulesTests
{
    private static Adisyon Open(int tableId = 1, decimal price = 100, int qty = 3)
    {
        var item = new OrderItem { Id = 10, MenuItemId = 5, MenuItemName = "Çay", UnitPrice = price, Quantity = qty, Status = OrderStatus.Hazirlaniyor, Notes = "az şekerli" };
        item.Options.Add(new OrderItemOption { Name = "Limon", ExtraPrice = 5 });
        var order = new Order { Id = 1, TableId = tableId, IsDraft = false, Status = OrderStatus.Hazirlaniyor };
        order.Items.Add(item);
        var adisyon = new Adisyon { Id = 1, TableId = tableId, Status = AdisyonStatus.Acik };
        adisyon.Orders.Add(order);
        return adisyon;
    }

    [Fact]
    public void Move_ToEmptyTable_Allowed()
    {
        Assert.Null(TableTransferRules.MoveOrMergeBlockReason(Open(), 2, null, sourceHasPendingVoid: false));
    }

    [Fact]
    public void Move_SameTable_Rejected()
    {
        Assert.NotNull(TableTransferRules.MoveOrMergeBlockReason(Open(), 1, null, false));
    }

    [Fact]
    public void Move_ClosedOrPackage_Rejected()
    {
        var closed = Open();
        closed.Status = AdisyonStatus.Odendi;
        Assert.NotNull(TableTransferRules.MoveOrMergeBlockReason(closed, 2, null, false));

        var merged = Open();
        merged.Status = AdisyonStatus.Birlestirildi;
        Assert.NotNull(TableTransferRules.MoveOrMergeBlockReason(merged, 2, null, false));

        var package = Open();
        package.OrderType = OrderType.GelAl;
        Assert.NotNull(TableTransferRules.MoveOrMergeBlockReason(package, 2, null, false));
    }

    [Fact]
    public void Merge_WithPendingVoid_Rejected_ButMoveAllowed()
    {
        var target = Open(2);
        Assert.NotNull(TableTransferRules.MoveOrMergeBlockReason(Open(), 2, target, sourceHasPendingVoid: true));
        Assert.Null(TableTransferRules.MoveOrMergeBlockReason(Open(), 2, null, sourceHasPendingVoid: true));
        Assert.Null(TableTransferRules.MoveOrMergeBlockReason(Open(), 2, target, sourceHasPendingVoid: false));
    }

    [Fact]
    public void ItemTransfer_ValidRequest_Allowed()
    {
        Assert.Null(TableTransferRules.ItemTransferBlockReason(Open(), 2, new Dictionary<int, int> { [10] = 1 }, false));
        Assert.Null(TableTransferRules.ItemTransferBlockReason(Open(), 2, new Dictionary<int, int> { [10] = 3 }, false));
    }

    [Fact]
    public void ItemTransfer_PaidDiscountedOrPendingRequests_Rejected()
    {
        var paid = Open();
        paid.Payments.Add(new Payment { Amount = 50 });
        Assert.NotNull(TableTransferRules.ItemTransferBlockReason(paid, 2, new Dictionary<int, int> { [10] = 1 }, false));

        var discounted = Open();
        discounted.DiscountAmount = 10;
        Assert.NotNull(TableTransferRules.ItemTransferBlockReason(discounted, 2, new Dictionary<int, int> { [10] = 1 }, false));

        var pendingDiscount = Open();
        pendingDiscount.DiscountRequests.Add(new DiscountRequest { Status = DiscountRequestStatus.Beklemede });
        Assert.NotNull(TableTransferRules.ItemTransferBlockReason(pendingDiscount, 2, new Dictionary<int, int> { [10] = 1 }, false));

        Assert.NotNull(TableTransferRules.ItemTransferBlockReason(Open(), 2, new Dictionary<int, int> { [10] = 1 }, sourceHasPendingVoid: true));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(-1)]
    public void ItemTransfer_InvalidQuantity_Rejected(int qty)
    {
        Assert.NotNull(TableTransferRules.ItemTransferBlockReason(Open(), 2, new Dictionary<int, int> { [10] = qty }, false));
    }

    [Fact]
    public void ItemTransfer_UnknownCancelledEmptyOrSameTable_Rejected()
    {
        Assert.NotNull(TableTransferRules.ItemTransferBlockReason(Open(), 2, new Dictionary<int, int> { [999] = 1 }, false));
        Assert.NotNull(TableTransferRules.ItemTransferBlockReason(Open(), 2, new Dictionary<int, int>(), false));
        Assert.NotNull(TableTransferRules.ItemTransferBlockReason(Open(), 1, new Dictionary<int, int> { [10] = 1 }, false));

        var cancelled = Open();
        cancelled.Orders[0].Items[0].Status = OrderStatus.IptalEdildi;
        Assert.NotNull(TableTransferRules.ItemTransferBlockReason(cancelled, 2, new Dictionary<int, int> { [10] = 1 }, false));
    }

    [Fact]
    public void SplitOff_KeepsPriceOptionsNoteAndTotal()
    {
        var source = Open();
        var item = source.Orders[0].Items[0];
        item.VatRate = 20;
        var before = item.LineTotal;

        var moved = TableTransferRules.SplitOff(item, 1);

        Assert.Equal(20, moved.VatRate);

        Assert.Equal(2, item.Quantity);
        Assert.Equal(1, moved.Quantity);
        Assert.Equal(item.UnitPrice, moved.UnitPrice);
        Assert.Equal(item.MenuItemId, moved.MenuItemId);
        Assert.Equal(item.MenuItemName, moved.MenuItemName);
        Assert.Equal(item.Status, moved.Status);
        Assert.Equal(item.Notes, moved.Notes);
        Assert.Equal(item.Options.Select(o => (o.Name, o.ExtraPrice)), moved.Options.Select(o => (o.Name, o.ExtraPrice)));
        Assert.Equal(before, item.LineTotal + moved.LineTotal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void SplitOff_RequiresPartialQuantity(int qty)
    {
        var item = Open().Orders[0].Items[0];
        Assert.Throws<ArgumentOutOfRangeException>(() => TableTransferRules.SplitOff(item, qty));
    }
}
