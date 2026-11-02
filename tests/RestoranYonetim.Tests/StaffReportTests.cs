using RestoranYonetim.Models;
using RestoranYonetim.Services.Reports;

namespace RestoranYonetim.Tests;

public class StaffReportTests
{
    private static WaiterOrderRow Order(string? user, int? adisyon, decimal live, OrderSource source = OrderSource.Garson, int cancelledQty = 0, decimal cancelledTotal = 0) =>
        new(user, source, adisyon, live, cancelledQty, cancelledTotal);

    [Fact]
    public void Qr_Orders_Grouped_Separately()
    {
        var rows = StaffReport.BuildWaiters(new[]
        {
            Order("Ali", 1, 100), Order(null, 2, 50, OrderSource.Musteri), Order("", 3, 30), Order("Ayşe", 4, 20, OrderSource.Musteri)
        }, []);

        var qr = Assert.Single(rows, r => r.Name == StaffReport.QrLabel);
        Assert.Equal(100m, qr.Revenue);
        Assert.Equal(3, qr.Orders);
        Assert.DoesNotContain(rows, r => r.Name == "Ayşe");
    }

    [Fact]
    public void Cancelled_Not_In_Revenue_But_Counted()
    {
        var ali = Assert.Single(StaffReport.BuildWaiters(new[] { Order("Ali", 1, 100, cancelledQty: 2, cancelledTotal: 80) }, []));

        Assert.Equal(100m, ali.Revenue);
        Assert.Equal(2, ali.CancelledQty);
        Assert.Equal(80m, ali.CancelledTotal);
    }

    [Fact]
    public void AvgTicket_Is_Revenue_Per_Adisyon()
    {
        var ali = Assert.Single(StaffReport.BuildWaiters(new[] { Order("Ali", 1, 100), Order("Ali", 1, 50), Order("Ali", 2, 150) }, []));

        Assert.Equal(2, ali.Adisyons);
        Assert.Equal(3, ali.Orders);
        Assert.Equal(150m, ali.AvgTicket);
    }

    [Fact]
    public void Null_Adisyon_Not_Counted()
    {
        var ali = Assert.Single(StaffReport.BuildWaiters(new[] { Order("Ali", null, 40) }, []));

        Assert.Equal(0, ali.Adisyons);
        Assert.Equal(0m, ali.AvgTicket);
    }

    [Fact]
    public void Discounts_Summed_Per_Requester()
    {
        var rows = StaffReport.BuildWaiters(new[] { Order("Ali", 1, 100) },
            new[] { new DiscountRow("Ali", 10), new DiscountRow("Ali", 15), new DiscountRow("Veli", 5) });

        var ali = Assert.Single(rows, r => r.Name == "Ali");
        Assert.Equal((2, 25m), (ali.Discounts, ali.DiscountTotal));
        var veli = Assert.Single(rows, r => r.Name == "Veli");
        Assert.Equal((0, 1, 5m), (veli.Orders, veli.Discounts, veli.DiscountTotal));
    }

    [Fact]
    public void Waiters_Ordered_By_Revenue_Desc()
    {
        var rows = StaffReport.BuildWaiters(new[] { Order("Ali", 1, 100), Order("Veli", 2, 300), Order("Can", 3, 200) }, []);

        Assert.Equal(new[] { "Veli", "Can", "Ali" }, rows.Select(r => r.Name));
    }

    [Fact]
    public void Cashier_Splits_Methods_And_Reversals()
    {
        var kasiyer = Assert.Single(StaffReport.BuildCashiers(new[]
        {
            new PaymentRow("Kasiyer", PaymentMethod.Nakit, 100), new PaymentRow("Kasiyer", PaymentMethod.KrediKarti, 250),
            new PaymentRow("Kasiyer", PaymentMethod.Nakit, 50), new PaymentRow("Kasiyer", PaymentMethod.Nakit, -30)
        }, []));

        Assert.Equal(400m, kasiyer.Collected);
        Assert.Equal(new[] { (PaymentMethod.Nakit, 150m), (PaymentMethod.KrediKarti, 250m) }, kasiyer.ByMethod);
        Assert.Equal(30m, kasiyer.Reversed);
    }

    [Fact]
    public void Cashier_Decider_Without_Payments_Gets_Row()
    {
        var rows = StaffReport.BuildCashiers(new[] { new PaymentRow("Kasiyer", PaymentMethod.Nakit, 100) },
            new[] { new DecisionRow("Müdür"), new DecisionRow("Müdür"), new DecisionRow("Kasiyer") });

        Assert.Equal((0m, 2), rows.Where(r => r.Name == "Müdür").Select(r => (r.Collected, r.Decisions)).Single());
        Assert.Equal(1, rows.Single(r => r.Name == "Kasiyer").Decisions);
        Assert.Equal("Kasiyer", rows[0].Name);
    }

    [Fact]
    public void Blank_Cashier_Name_Is_Dash()
    {
        Assert.Equal("—", Assert.Single(StaffReport.BuildCashiers(new[] { new PaymentRow(" ", PaymentMethod.Nakit, 10) }, [])).Name);
    }
}
