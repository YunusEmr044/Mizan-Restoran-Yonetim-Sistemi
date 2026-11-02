using RestoranYonetim.Models;

namespace RestoranYonetim.Services;

public sealed record WorkTotal(string UserId, string UserName, double Hours, int Entries, bool HasOpenEntry);

// Vardiya, mesai ve izin kuralları.
public static class PersonnelRules
{
    public const int MaxShiftHours = 16;
    public const int MaxLeaveDays = 60;

    public static DateTime WeekStart(DateTime date) => date.Date.AddDays(-(((int)date.DayOfWeek + 6) % 7));

    // Bitiş başlangıçtan küçükse vardiya gece yarısını geçer.
    public static TimeSpan ShiftLength(TimeSpan start, TimeSpan end) =>
        end >= start ? end - start : end + TimeSpan.FromDays(1) - start;

    public static string? ShiftError(TimeSpan start, TimeSpan end, IEnumerable<(TimeSpan Start, TimeSpan End)> sameDay)
    {
        var length = ShiftLength(start, end);
        if (length == TimeSpan.Zero)
        {
            return "Bitiş saati başlangıç saatinden farklı olmalı.";
        }
        if (length > TimeSpan.FromHours(MaxShiftHours))
        {
            return $"Bir vardiya en fazla {MaxShiftHours} saat olabilir.";
        }
        var overlaps = sameDay.Any(s => start < s.Start + ShiftLength(s.Start, s.End) && s.Start < start + length);
        return overlaps ? "Bu personelin o gün çakışan bir vardiyası var." : null;
    }

    public static string? LeaveError(DateTime start, DateTime end, IEnumerable<(DateTime Start, DateTime End)> existing)
    {
        if (end.Date < start.Date)
        {
            return "Bitiş tarihi başlangıç tarihinden önce olamaz.";
        }
        if ((end.Date - start.Date).TotalDays + 1 > MaxLeaveDays)
        {
            return $"Tek seferde en fazla {MaxLeaveDays} günlük izin istenebilir.";
        }
        return existing.Any(e => start.Date <= e.End.Date && e.Start.Date <= end.Date)
            ? "Bu tarihlerde başka bir izin talebi var."
            : null;
    }

    // Çalışma süresi [from, to) aralığına kırpılır; çıkışı girilmemiş kayıtlar süreye eklenmez, ayrıca işaretlenir.
    public static IReadOnlyList<WorkTotal> Totals(IEnumerable<TimeEntry> entries, DateTime from, DateTime to) =>
        entries.GroupBy(e => e.UserId)
            .Select(g => new WorkTotal(
                g.Key,
                g.First().UserName,
                Math.Round(g.Where(e => e.ClockOut != null)
                    .Sum(e => Math.Max(0, ((e.ClockOut!.Value < to ? e.ClockOut.Value : to) - (e.ClockIn > from ? e.ClockIn : from)).TotalHours)), 2),
                g.Count(),
                g.Any(e => e.ClockOut == null)))
            .OrderBy(t => t.UserName)
            .ToList();
}
