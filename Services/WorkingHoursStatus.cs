using RestoranYonetim.Models;

namespace RestoranYonetim.Services;

// "Şu an açığız/kapalıyız" rozetini WorkingHoursDay'den hesaplar. Gece yarısını geçen saatlerde
// (ör. 18:00-02:00) hem bugünün hem dünün kaydı kontrol edilmesi gerekir.
public static class WorkingHoursStatus
{
    public record Result(bool IsOpen, string StatusText, string? TodayHoursText, string? ClosingOrOpeningText);

    public static Result Compute(List<WorkingHoursDay> days, DateTime now)
    {
        if (days == null || days.Count == 0)
        {
            return new Result(false, "Çalışma saatleri henüz tanımlanmadı", null, null);
        }

        var today = days.FirstOrDefault(d => d.DayOfWeek == now.DayOfWeek);
        var yesterday = days.FirstOrDefault(d => d.DayOfWeek == now.DayOfWeek - 1 || (now.DayOfWeek == DayOfWeek.Sunday && d.DayOfWeek == DayOfWeek.Saturday));

        var currentTime = now.TimeOfDay;

        // Bugünün saatleri içinde miyiz? (normal aralık, veya gece yarısını geçen aralığın ilk yarısı)
        if (today != null && !today.IsClosed)
        {
            var crossesMidnight = today.CloseTime <= today.OpenTime;
            if (!crossesMidnight)
            {
                if (currentTime >= today.OpenTime && currentTime < today.CloseTime)
                {
                    return new Result(true, "Şu an açığız", FormatHours(today), $"Bugün {today.CloseTime:hh\\:mm}'da kapanıyoruz");
                }
            }
            else
            {
                if (currentTime >= today.OpenTime)
                {
                    return new Result(true, "Şu an açığız", FormatHours(today), $"Yarın {today.CloseTime:hh\\:mm}'da kapanıyoruz");
                }
            }
        }

        // Dünün gece yarısını geçen kapanışı bugüne taşmış olabilir.
        if (yesterday != null && !yesterday.IsClosed && yesterday.CloseTime <= yesterday.OpenTime)
        {
            if (currentTime < yesterday.CloseTime)
            {
                return new Result(true, "Şu an açığız", today != null ? FormatHours(today) : null, $"Bugün {yesterday.CloseTime:hh\\:mm}'da kapanıyoruz");
            }
        }

        if (today != null && !today.IsClosed && currentTime < today.OpenTime)
        {
            return new Result(false, "Şu an kapalıyız", FormatHours(today), $"Bugün {today.OpenTime:hh\\:mm}'de açılıyoruz");
        }

        var nextOpenDay = FindNextOpenDay(days, now.DayOfWeek);
        return new Result(false, "Şu an kapalıyız", today != null ? FormatHours(today) : null, nextOpenDay != null ? $"{TurkishDayName(nextOpenDay.DayOfWeek)} günü {nextOpenDay.OpenTime:hh\\:mm}'de açılıyoruz" : null);
    }

    private static WorkingHoursDay? FindNextOpenDay(List<WorkingHoursDay> days, DayOfWeek from)
    {
        for (var i = 1; i <= 7; i++)
        {
            var day = (DayOfWeek)(((int)from + i) % 7);
            var match = days.FirstOrDefault(d => d.DayOfWeek == day);
            if (match != null && !match.IsClosed)
            {
                return match;
            }
        }
        return null;
    }

    private static string FormatHours(WorkingHoursDay day) =>
        day.IsClosed ? "Kapalı" : $"{day.OpenTime:hh\\:mm} - {day.CloseTime:hh\\:mm}";

    public static string TurkishDayName(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => "Pazartesi",
        DayOfWeek.Tuesday => "Salı",
        DayOfWeek.Wednesday => "Çarşamba",
        DayOfWeek.Thursday => "Perşembe",
        DayOfWeek.Friday => "Cuma",
        DayOfWeek.Saturday => "Cumartesi",
        _ => "Pazar"
    };
}
