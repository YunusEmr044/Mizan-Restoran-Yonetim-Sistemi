using RestoranYonetim.Models;
using RestoranYonetim.Services;

namespace RestoranYonetim.Tests;

public class VoidRulesTests
{
    private static Adisyon Build(AdisyonStatus status, decimal[] itemPrices, params decimal[] payments)
    {
        var order = new Order { Id = 1, IsDraft = false, Status = OrderStatus.Yeni };
        var id = 1;
        foreach (var price in itemPrices)
        {
            order.Items.Add(new OrderItem { Id = id++, OrderId = 1, UnitPrice = price, Quantity = 1, Status = OrderStatus.Yeni });
        }
        var adisyon = new Adisyon { Id = 10, Status = status, Orders = { order } };
        var pid = 100;
        foreach (var amount in payments)
        {
            adisyon.Payments.Add(new Payment { Id = pid++, AdisyonId = 10, Amount = amount });
        }
        return adisyon;
    }

    [Theory]
    [InlineData(OrderStatus.Yeni, true)]
    [InlineData(OrderStatus.Hazirlaniyor, false)]
    [InlineData(OrderStatus.Hazir, false)]
    [InlineData(OrderStatus.ServisEdildi, false)]
    public void CanCancelItemDirectly_OnlyNew(OrderStatus status, bool expected)
    {
        Assert.Equal(expected, VoidRules.CanCancelItemDirectly(new OrderItem { Status = status }));
    }

    [Fact]
    public void CancelItem_ClosedAdisyon_Blocked()
    {
        var a = Build(AdisyonStatus.Odendi, new[] { 100m });
        Assert.NotNull(VoidRules.CancelItemBlockReason(a, a.Orders[0].Items[0]));
    }

    [Fact]
    public void CancelItem_AlreadyCancelled_Blocked()
    {
        var a = Build(AdisyonStatus.Acik, new[] { 100m });
        a.Orders[0].Items[0].Status = OrderStatus.IptalEdildi;
        Assert.NotNull(VoidRules.CancelItemBlockReason(a, a.Orders[0].Items[0]));
    }

    [Fact]
    public void CancelItem_WouldLeavePaymentAboveTotal_Blocked()
    {
        var a = Build(AdisyonStatus.KismiOdendi, new[] { 150m, 150m }, 200m);
        Assert.NotNull(VoidRules.CancelItemBlockReason(a, a.Orders[0].Items[0]));
    }

    [Fact]
    public void CancelItem_PaymentEqualsNewTotal_Allowed()
    {
        var a = Build(AdisyonStatus.KismiOdendi, new[] { 200m, 100m }, 200m);
        Assert.Null(VoidRules.CancelItemBlockReason(a, a.Orders[0].Items[1]));
    }

    [Fact]
    public void Refund_NotPaid_Blocked()
    {
        Assert.NotNull(VoidRules.RefundBlockReason(Build(AdisyonStatus.Kasada, new[] { 100m }, 100m), 50m));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(100.01)]
    public void Refund_InvalidAmount_Blocked(decimal amount)
    {
        Assert.NotNull(VoidRules.RefundBlockReason(Build(AdisyonStatus.Odendi, new[] { 100m }, 100m), amount));
    }

    [Fact]
    public void Refund_ExactMax_Allowed()
    {
        Assert.Null(VoidRules.RefundBlockReason(Build(AdisyonStatus.Odendi, new[] { 100m }, 100m), 100m));
    }

    [Fact]
    public void MaxRefund_SubtractsPreviousRefunds()
    {
        Assert.Equal(60m, VoidRules.MaxRefund(Build(AdisyonStatus.Odendi, new[] { 100m }, 100m, -40m)));
    }

    [Fact]
    public void PaymentVoid_ClosedAdisyon_Blocked()
    {
        var a = Build(AdisyonStatus.Odendi, new[] { 100m }, 100m);
        Assert.NotNull(VoidRules.PaymentVoidBlockReason(a, a.Payments[0]));
    }

    [Fact]
    public void PaymentVoid_ReversalEntry_Blocked()
    {
        var a = Build(AdisyonStatus.Kasada, new[] { 100m }, 100m, -100m);
        a.Payments[1].ReversesPaymentId = a.Payments[0].Id;
        Assert.NotNull(VoidRules.PaymentVoidBlockReason(a, a.Payments[1]));
    }

    [Fact]
    public void PaymentVoid_AlreadyReversed_Blocked()
    {
        var a = Build(AdisyonStatus.Kasada, new[] { 100m }, 100m, -100m);
        a.Payments[1].ReversesPaymentId = a.Payments[0].Id;
        Assert.NotNull(VoidRules.PaymentVoidBlockReason(a, a.Payments[0]));
    }

    [Fact]
    public void PaymentVoid_OtherAdisyonPayment_Blocked()
    {
        var a = Build(AdisyonStatus.Kasada, new[] { 100m });
        Assert.NotNull(VoidRules.PaymentVoidBlockReason(a, new Payment { Id = 999, AdisyonId = 77, Amount = 50m }));
    }

    [Fact]
    public void PaymentVoid_Normal_Allowed()
    {
        var a = Build(AdisyonStatus.Kasada, new[] { 100m }, 100m);
        Assert.Null(VoidRules.PaymentVoidBlockReason(a, a.Payments[0]));
    }

    [Fact]
    public void AdisyonCancel_Rules()
    {
        Assert.NotNull(VoidRules.AdisyonCancelBlockReason(Build(AdisyonStatus.Iptal, new[] { 100m })));
        Assert.NotNull(VoidRules.AdisyonCancelBlockReason(Build(AdisyonStatus.KismiOdendi, new[] { 100m }, 50m)));
        Assert.Null(VoidRules.AdisyonCancelBlockReason(Build(AdisyonStatus.Acik, new[] { 100m })));
    }

    [Fact]
    public void StatusAfterPaymentVoid()
    {
        Assert.Equal(AdisyonStatus.Kasada, VoidRules.StatusAfterPaymentVoid(Build(AdisyonStatus.Kasada, new[] { 100m }, 100m, -100m)));
        Assert.Equal(AdisyonStatus.KismiOdendi, VoidRules.StatusAfterPaymentVoid(Build(AdisyonStatus.Kasada, new[] { 100m }, 60m, 40m, -40m)));
    }

    [Fact]
    public void StatusAfterRefund()
    {
        Assert.Equal(AdisyonStatus.Iade, VoidRules.StatusAfterRefund(Build(AdisyonStatus.Odendi, new[] { 100m }, 100m, -100m)));
        Assert.Equal(AdisyonStatus.Odendi, VoidRules.StatusAfterRefund(Build(AdisyonStatus.Odendi, new[] { 100m }, 100m, -30m)));
    }
}
