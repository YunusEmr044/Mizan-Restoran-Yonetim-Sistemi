using RestoranYonetim.Models.Reports;

namespace RestoranYonetim.Services.Reports;

// Seçilen dönemi aynı uzunluktaki önceki dönemle kıyaslar.
public static class PeriodComparison
{
    public static decimal? Change(decimal current, decimal previous) =>
        previous == 0 ? null : Math.Round((current - previous) / previous * 100, 1, MidpointRounding.AwayFromZero);

    public static (DateTime Start, DateTime End) Previous(DateTime start, DateTime endExclusive) =>
        (start - (endExclusive - start), start);

    public static IReadOnlyList<decimal> Series(IEnumerable<DailySalesRow> daily, DateTime start, int days)
    {
        var totals = daily.ToDictionary(d => d.Date.Date, d => d.Total);
        return Enumerable.Range(0, days).Select(i => totals.GetValueOrDefault(start.Date.AddDays(i))).ToList();
    }
}
