using RestoranYonetim.Services;
using RestoranYonetim.Services.Reports;

namespace RestoranYonetim.Models.Reports;

public sealed record StaffReportView(DateTime Start, DateTime End, IReadOnlyList<WaiterStats> Waiters, IReadOnlyList<CashierStats>? Cashiers);

public sealed record LineChartModel(IReadOnlyList<DateTime> Days, IReadOnlyList<decimal> Current, IReadOnlyList<decimal> Previous, string CurrentLabel, string PreviousLabel);

public sealed record ComparisonView(
    DateTime Start,
    DateTime End,
    DateTime PreviousStart,
    DateTime PreviousEnd,
    SalesSummary Current,
    SalesSummary Previous,
    LineChartModel Chart,
    LossSummary Losses,
    IReadOnlyList<ReasonStat> Reasons);
