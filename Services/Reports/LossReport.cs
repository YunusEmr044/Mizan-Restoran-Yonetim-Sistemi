using System.Globalization;

namespace RestoranYonetim.Services.Reports;

public sealed record LossLine(string Label, int Count, decimal Amount);

public sealed record LossSummary(IReadOnlyList<LossLine> Lines, decimal Total, decimal? ShareOfRevenue);

public enum ReasonKind { Iptal, Indirim }

public sealed record ReasonRow(ReasonKind Kind, string? Reason, decimal Amount);

public sealed record ReasonStat(string Reason, ReasonKind Kind, int Count, decimal Amount);

// İptal, indirim, iade ve ödeme iptali kaynaklı kayıplar ve en sık gerekçeler.
public static class LossReport
{
    public const string UnspecifiedReason = "Belirtilmemiş";
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    public static LossSummary Build((int Count, decimal Total) cancelled, (int Count, decimal Total) discounts,
        (int Count, decimal Total) refunds, (int Count, decimal Total) reversals, decimal revenue)
    {
        var lines = new[]
        {
            new LossLine("İptal edilen kalemler", cancelled.Count, cancelled.Total),
            new LossLine("Onaylanan indirimler", discounts.Count, discounts.Total),
            new LossLine("İadeler", refunds.Count, refunds.Total),
            new LossLine("Ödeme iptalleri", reversals.Count, reversals.Total)
        };
        var total = lines.Sum(l => l.Amount);
        return new LossSummary(lines, total, revenue == 0 ? null : Math.Round(total / revenue * 100, 1, MidpointRounding.AwayFromZero));
    }

    public static IReadOnlyList<ReasonStat> TopReasons(IEnumerable<ReasonRow> rows, int take = 10) =>
        rows.Select(r => (r.Kind, Text: string.IsNullOrWhiteSpace(r.Reason) ? UnspecifiedReason : r.Reason.Trim(), r.Amount))
            .GroupBy(r => (r.Kind, Key: r.Text.ToLower(Turkish)))
            .Select(g => new ReasonStat(g.First().Text, g.Key.Kind, g.Count(), g.Sum(r => r.Amount)))
            .OrderByDescending(r => r.Count).ThenByDescending(r => r.Amount).ThenBy(r => r.Reason)
            .Take(take)
            .ToList();
}
