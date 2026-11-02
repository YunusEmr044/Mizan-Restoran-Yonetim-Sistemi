using RestoranYonetim.Models;
using RestoranYonetim.Services;

namespace RestoranYonetim.Tests;

public class PackageFlowTests
{
    private static Adisyon Package(OrderType type, OrderStatus itemStatus = OrderStatus.Hazir)
    {
        var a = new Adisyon { Id = 1, OrderType = type, Status = AdisyonStatus.Acik, Package = new PackageInfo { CustomerName = "Ahmet" } };
        var order = new Order { IsDraft = false, Status = itemStatus };
        order.Items.Add(new OrderItem { MenuItemName = "Lahmacun", UnitPrice = 120, Quantity = 1, Status = itemStatus });
        a.Orders.Add(order);
        return a;
    }

    [Fact]
    public void HandOver_OnlyTakeawayOnce()
    {
        Assert.Null(PackageFlow.HandOverBlockReason(Package(OrderType.GelAl)));
        Assert.NotNull(PackageFlow.HandOverBlockReason(Package(OrderType.AdreseTeslim)));

        var done = Package(OrderType.GelAl);
        done.Package!.HandedOverAt = DateTime.Now;
        Assert.NotNull(PackageFlow.HandOverBlockReason(done));
    }

    [Fact]
    public void Dispatch_NeedsCourierAndDelivery()
    {
        Assert.NotNull(PackageFlow.DispatchBlockReason(Package(OrderType.AdreseTeslim), null));
        Assert.NotNull(PackageFlow.DispatchBlockReason(Package(OrderType.AdreseTeslim), "  "));
        Assert.Null(PackageFlow.DispatchBlockReason(Package(OrderType.AdreseTeslim), "Mehmet"));
        Assert.NotNull(PackageFlow.DispatchBlockReason(Package(OrderType.GelAl), "Mehmet"));
        Assert.NotNull(PackageFlow.DispatchBlockReason(Package(OrderType.AdreseTeslim), new string('k', 101)));
    }

    [Fact]
    public void Deliver_OnlyAfterDispatch()
    {
        var p = Package(OrderType.AdreseTeslim);
        Assert.NotNull(PackageFlow.DeliverBlockReason(p));
        p.Package!.DispatchedAt = DateTime.Now;
        Assert.Null(PackageFlow.DeliverBlockReason(p));
        p.Package.DeliveredAt = DateTime.Now;
        Assert.NotNull(PackageFlow.DeliverBlockReason(p));
    }

    [Fact]
    public void PaidPackage_CanStillBeHandedOverAndDelivered_ButCancelledCannot()
    {
        var paid = Package(OrderType.GelAl);
        paid.Status = AdisyonStatus.Odendi;
        Assert.Null(PackageFlow.HandOverBlockReason(paid));

        var paidDelivery = Package(OrderType.AdreseTeslim);
        paidDelivery.Status = AdisyonStatus.Odendi;
        Assert.True(PackageFlow.CanDispatch(paidDelivery));
        Assert.Null(PackageFlow.DispatchBlockReason(paidDelivery, "Mehmet"));

        var cancelled = Package(OrderType.GelAl);
        cancelled.Status = AdisyonStatus.Iptal;
        Assert.NotNull(PackageFlow.HandOverBlockReason(cancelled));
        Assert.False(PackageFlow.CanDispatch(new Adisyon { OrderType = OrderType.AdreseTeslim, Status = AdisyonStatus.Iade }));
        Assert.False(PackageFlow.CanDispatch(Package(OrderType.GelAl)));
    }

    [Fact]
    public void Cancel_OnlyWhenNothingSentOrPaid()
    {
        var empty = new Adisyon { OrderType = OrderType.GelAl, Status = AdisyonStatus.Acik };
        Assert.Null(PackageFlow.CancelBlockReason(empty));
        Assert.NotNull(PackageFlow.CancelBlockReason(Package(OrderType.GelAl)));

        var paid = new Adisyon { OrderType = OrderType.GelAl, Status = AdisyonStatus.Acik };
        paid.Payments.Add(new Payment { Amount = 10 });
        Assert.NotNull(PackageFlow.CancelBlockReason(paid));
    }

    [Fact]
    public void Stage_ReflectsKitchenAndHandover()
    {
        Assert.Equal("Hazırlanıyor", PackageFlow.Stage(Package(OrderType.GelAl, OrderStatus.Hazirlaniyor)));
        Assert.Equal("Hazır", PackageFlow.Stage(Package(OrderType.GelAl, OrderStatus.Hazir)));

        var handed = Package(OrderType.GelAl);
        handed.Package!.HandedOverAt = DateTime.Now;
        Assert.Equal("Teslim alındı", PackageFlow.Stage(handed));

        var onRoad = Package(OrderType.AdreseTeslim);
        onRoad.Package!.DispatchedAt = DateTime.Now;
        Assert.Equal("Yolda", PackageFlow.Stage(onRoad));
        onRoad.Package.DeliveredAt = DateTime.Now;
        Assert.Equal("Teslim edildi", PackageFlow.Stage(onRoad));

        Assert.Equal("Ürün bekleniyor", PackageFlow.Stage(new Adisyon { OrderType = OrderType.GelAl }));
    }
}
