using RestoranYonetim.Models;
using RestoranYonetim.Services;

namespace RestoranYonetim.Tests;

public class SalesReportTests
{
    private static readonly DateTime Day1 = new(2026, 10, 1, 20, 0, 0);
    private static readonly DateTime Day3 = new(2026, 10, 3, 13, 0, 0);

    [Fact]
    public void RefundCountsOnTheDayItIsMade()
    {
        var report = SalesReport.Build(new[] { (Day1, 1000m, OrderType.Masa) }, new[] { (Day3, 200m) });

        var first = Assert.Single(report.Daily, d => d.Date == Day1.Date);
        Assert.Equal(1000m, first.Total);
        Assert.Equal(1, first.Count);
        Assert.Equal(0m, first.Refunds);

        var refundDay = Assert.Single(report.Daily, d => d.Date == Day3.Date);
        Assert.Equal(-200m, refundDay.Total);
        Assert.Equal(0, refundDay.Count);
        Assert.Equal(200m, refundDay.Refunds);

        Assert.Equal(800m, report.TotalRevenue);
        Assert.Equal(200m, report.TotalRefunds);
        Assert.Equal(1, report.TotalCount);
        Assert.Equal(1000m, report.AvgTicket);
    }

    [Fact]
    public void SameDayRefundNetsOff()
    {
        var report = SalesReport.Build(new[] { (Day1, 500m, OrderType.Masa), (Day1.AddHours(1), 300m, OrderType.Masa) }, new[] { (Day1.AddHours(2), 100m) });

        var day = Assert.Single(report.Daily);
        Assert.Equal(700m, day.Total);
        Assert.Equal(2, day.Count);
        Assert.Equal(400m, report.AvgTicket);
    }

    [Fact]
    public void BreakdownByOrderType()
    {
        var report = SalesReport.Build(new[] { (Day1, 1000m, OrderType.Masa), (Day1, 300m, OrderType.GelAl), (Day3, 200m, OrderType.GelAl) }, []);

        Assert.Equal(new[] { (OrderType.Masa, 1, 1000m), (OrderType.GelAl, 2, 500m) }, report.ByType);
        Assert.Equal(1500m, report.TotalRevenue);
    }

    [Fact]
    public void EmptyRangeHasNoAverage()
    {
        var report = SalesReport.Build([], []);

        Assert.Empty(report.Daily);
        Assert.Equal(0m, report.AvgTicket);
        Assert.Equal(0m, report.TotalRevenue);
    }

    [Theory]
    [InlineData(-200, null, true)]
    [InlineData(-200, 15, false)]
    [InlineData(200, null, false)]
    public void IsRefund_OnlyNegativeWithoutReversedPayment(double amount, int? reversesPaymentId, bool expected)
    {
        Assert.Equal(expected, SalesReport.IsRefund((decimal)amount, reversesPaymentId));
    }
}
