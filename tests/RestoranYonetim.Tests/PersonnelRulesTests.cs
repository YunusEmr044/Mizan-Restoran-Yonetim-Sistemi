using RestoranYonetim.Models;
using RestoranYonetim.Services;

namespace RestoranYonetim.Tests;

public class PersonnelRulesTests
{
    private static TimeSpan T(int h, int m = 0) => new(h, m, 0);

    [Theory]
    [InlineData("2026-11-05", "2026-11-02")]
    [InlineData("2026-11-02", "2026-11-02")]
    [InlineData("2026-11-08", "2026-11-02")]
    public void Week_Starts_On_Monday(string date, string monday)
    {
        Assert.Equal(DateTime.Parse(monday), PersonnelRules.WeekStart(DateTime.Parse(date)));
    }

    [Fact]
    public void Overnight_Shift_Length_Wraps_Midnight()
    {
        Assert.Equal(T(8), PersonnelRules.ShiftLength(T(18), T(2)));
        Assert.Equal(T(8, 30), PersonnelRules.ShiftLength(T(9), T(17, 30)));
    }

    [Fact]
    public void Shift_Must_Have_Length_And_Not_Overlap()
    {
        Assert.NotNull(PersonnelRules.ShiftError(T(9), T(9), Array.Empty<(TimeSpan, TimeSpan)>()));
        Assert.NotNull(PersonnelRules.ShiftError(T(12), T(20), new[] { (T(9), T(14)) }));
        Assert.Null(PersonnelRules.ShiftError(T(14), T(22), new[] { (T(9), T(14)) }));
        Assert.NotNull(PersonnelRules.ShiftError(T(23), T(3), new[] { (T(22), T(23, 30)) }));
    }

    [Fact]
    public void Leave_Dates_Are_Validated()
    {
        var none = Array.Empty<(DateTime, DateTime)>();
        Assert.NotNull(PersonnelRules.LeaveError(new DateTime(2026, 11, 10), new DateTime(2026, 11, 9), none));
        Assert.NotNull(PersonnelRules.LeaveError(new DateTime(2026, 11, 1), new DateTime(2027, 1, 15), none));
        Assert.NotNull(PersonnelRules.LeaveError(new DateTime(2026, 11, 10), new DateTime(2026, 11, 12), new[] { (new DateTime(2026, 11, 12), new DateTime(2026, 11, 14)) }));
        Assert.Null(PersonnelRules.LeaveError(new DateTime(2026, 11, 10), new DateTime(2026, 11, 11), new[] { (new DateTime(2026, 11, 12), new DateTime(2026, 11, 14)) }));
    }

    [Fact]
    public void Totals_Clip_To_Range_And_Flag_Open_Entries()
    {
        var entries = new[]
        {
            new TimeEntry { UserId = "a", UserName = "Ali", ClockIn = new DateTime(2026, 11, 2, 9, 0, 0), ClockOut = new DateTime(2026, 11, 2, 17, 0, 0) },
            new TimeEntry { UserId = "a", UserName = "Ali", ClockIn = new DateTime(2026, 11, 8, 20, 0, 0), ClockOut = new DateTime(2026, 11, 9, 2, 0, 0) },
            new TimeEntry { UserId = "b", UserName = "Ayşe", ClockIn = new DateTime(2026, 11, 3, 10, 0, 0) }
        };
        var totals = PersonnelRules.Totals(entries, new DateTime(2026, 11, 2), new DateTime(2026, 11, 9));
        var ali = totals.Single(t => t.UserId == "a");
        Assert.Equal(12, ali.Hours);
        Assert.Equal(2, ali.Entries);
        var ayse = totals.Single(t => t.UserId == "b");
        Assert.Equal(0, ayse.Hours);
        Assert.True(ayse.HasOpenEntry);
    }
}
