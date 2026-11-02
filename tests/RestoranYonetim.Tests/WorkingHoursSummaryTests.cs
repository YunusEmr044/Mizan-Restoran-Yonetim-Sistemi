using RestoranYonetim.Models;
using RestoranYonetim.Services;

namespace RestoranYonetim.Tests;

public class WorkingHoursSummaryTests
{
    [Fact]
    public void Summarize_ListsDaysFromMondayWithHoursOrClosed()
    {
        var days = new List<WorkingHoursDay>
        {
            new() { DayOfWeek = DayOfWeek.Sunday, IsClosed = true },
            new() { DayOfWeek = DayOfWeek.Monday, OpenTime = new TimeSpan(9, 0, 0), CloseTime = new TimeSpan(23, 30, 0) }
        };

        Assert.Equal("Pazartesi 09:00 - 23:30, Pazar Kapalı", WorkingHoursStatus.Summarize(days));
    }

    [Fact]
    public void Summarize_EmptyReturnsNull()
    {
        Assert.Null(WorkingHoursStatus.Summarize(new List<WorkingHoursDay>()));
    }
}
