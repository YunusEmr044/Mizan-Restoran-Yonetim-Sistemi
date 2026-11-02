using RestoranYonetim.Models.Reports;
using RestoranYonetim.Services.Reports;

namespace RestoranYonetim.Tests;

public class PeriodComparisonTests
{
    [Fact]
    public void Change_Previous_Zero_Is_Null()
    {
        Assert.Null(PeriodComparison.Change(100, 0));
    }

    [Theory]
    [InlineData(150, 100, 50.0)]
    [InlineData(75, 100, -25.0)]
    [InlineData(1, 3, -66.7)]
    [InlineData(0, 100, -100.0)]
    public void Change_Up_And_Down(double current, double previous, double expected)
    {
        Assert.Equal((decimal)expected, PeriodComparison.Change((decimal)current, (decimal)previous));
    }

    [Fact]
    public void Previous_Has_Same_Length()
    {
        var (start, end) = PeriodComparison.Previous(new DateTime(2026, 11, 4), new DateTime(2026, 11, 11));

        Assert.Equal(new DateTime(2026, 10, 28), start);
        Assert.Equal(new DateTime(2026, 11, 4), end);
    }

    [Fact]
    public void Series_Fills_Missing_Days_With_Zero()
    {
        var daily = new[]
        {
            new DailySalesRow { Date = new DateTime(2026, 11, 4), Total = 100 },
            new DailySalesRow { Date = new DateTime(2026, 11, 6), Total = 300 }
        };

        Assert.Equal(new[] { 100m, 0m, 300m, 0m }, PeriodComparison.Series(daily, new DateTime(2026, 11, 4), 4));
    }

    [Fact]
    public void Series_Single_Day()
    {
        Assert.Equal(new[] { 0m }, PeriodComparison.Series([], new DateTime(2026, 11, 4), 1));
    }
}

public class LossReportTests
{
    [Fact]
    public void Loss_Total_And_Share()
    {
        var summary = LossReport.Build((3, 120), (2, 30), (1, 50), (1, 100), revenue: 3000);

        Assert.Equal(new[] { "İptal edilen kalemler", "Onaylanan indirimler", "İadeler", "Ödeme iptalleri" }, summary.Lines.Select(l => l.Label));
        Assert.Equal(300m, summary.Total);
        Assert.Equal(10.0m, summary.ShareOfRevenue);
        Assert.Null(LossReport.Build((0, 0), (0, 0), (0, 0), (0, 0), revenue: 0).ShareOfRevenue);
    }

    [Fact]
    public void Reasons_Merge_Case_And_Trim()
    {
        var top = LossReport.TopReasons(new[]
        {
            new ReasonRow(ReasonKind.Iptal, "Müşteri vazgeçti", 50), new ReasonRow(ReasonKind.Iptal, " müşteri VAZGEÇTİ ", 30)
        });

        var only = Assert.Single(top);
        Assert.Equal(("Müşteri vazgeçti", 2, 80m), (only.Reason, only.Count, only.Amount));
    }

    [Fact]
    public void Blank_Reason_Is_Unspecified()
    {
        Assert.Equal("Belirtilmemiş", Assert.Single(LossReport.TopReasons(new[] { new ReasonRow(ReasonKind.Indirim, "  ", 10), new ReasonRow(ReasonKind.Indirim, null, 5) })).Reason);
    }

    [Fact]
    public void Reasons_Same_Text_Different_Kind_Stay_Separate()
    {
        Assert.Equal(2, LossReport.TopReasons(new[] { new ReasonRow(ReasonKind.Iptal, "Hata", 10), new ReasonRow(ReasonKind.Indirim, "Hata", 10) }).Count);
    }

    [Fact]
    public void Reasons_Top_N_And_Order()
    {
        var rows = Enumerable.Range(1, 12).SelectMany(i => Enumerable.Repeat(new ReasonRow(ReasonKind.Iptal, $"Neden {i}", i), i % 3 + 1)).ToList();
        rows.Add(new ReasonRow(ReasonKind.Indirim, "Büyük", 500));

        var top = LossReport.TopReasons(rows, 10);

        Assert.Equal(10, top.Count);
        Assert.Equal("Neden 11", top[0].Reason);
        Assert.True(top.Zip(top.Skip(1)).All(p => p.First.Count > p.Second.Count || p.First.Count == p.Second.Count && p.First.Amount >= p.Second.Amount));
    }
}
