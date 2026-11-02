using System.Globalization;
using RestoranYonetim.Models;
using RestoranYonetim.Services.I18n;

namespace RestoranYonetim.Services;

// "Şu an açığız/kapalıyız" rozetini WorkingHoursDay'den hesaplar. Gece yarısını geçen saatlerde
// (ör. 18:00-02:00) hem bugünün hem dünün kaydı kontrol edilmesi gerekir. Metinler verilen sözlükle müşterinin diline çevrilir.
public static class WorkingHoursStatus
{
    public record Result(bool IsOpen, string StatusText, string? TodayHoursText, string? ClosingOrOpeningText);

    public static Result Compute(List<WorkingHoursDay> days, DateTime now, TextLocalizer? t = null)
    {
        string L(string key, params object[] args) => t == null ? string.Format(CultureInfo.CurrentCulture, key, args) : t[key, args];

        if (days == null || days.Count == 0)
        {
            return new Result(false, L("Çalışma saatleri henüz tanımlanmadı"), null, null);
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
                    return new Result(true, L("Şu an açığız"), FormatHours(today, t), L("Bugün {0}'da kapanıyoruz", Hm(today.CloseTime)));
                }
            }
            else
            {
                if (currentTime >= today.OpenTime)
                {
                    return new Result(true, L("Şu an açığız"), FormatHours(today, t), L("Yarın {0}'da kapanıyoruz", Hm(today.CloseTime)));
                }
            }
        }

        // Dünün gece yarısını geçen kapanışı bugüne taşmış olabilir.
        if (yesterday != null && !yesterday.IsClosed && yesterday.CloseTime <= yesterday.OpenTime)
        {
            if (currentTime < yesterday.CloseTime)
            {
                return new Result(true, L("Şu an açığız"), today != null ? FormatHours(today, t) : null, L("Bugün {0}'da kapanıyoruz", Hm(yesterday.CloseTime)));
            }
        }

        if (today != null && !today.IsClosed && currentTime < today.OpenTime)
        {
            return new Result(false, L("Şu an kapalıyız"), FormatHours(today, t), L("Bugün {0}'de açılıyoruz", Hm(today.OpenTime)));
        }

        var nextOpenDay = FindNextOpenDay(days, now.DayOfWeek);
        return new Result(false, L("Şu an kapalıyız"), today != null ? FormatHours(today, t) : null, nextOpenDay != null ? L("{0} günü {1}'de açılıyoruz", DayName(nextOpenDay.DayOfWeek), Hm(nextOpenDay.OpenTime)) : null);
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

    public static string? Summarize(List<WorkingHoursDay> days)
    {
        if (days.Count == 0)
        {
            return null;
        }

        return string.Join(", ", days
            .OrderBy(d => ((int)d.DayOfWeek + 6) % 7)
            .Select(d => $"{TurkishDayName(d.DayOfWeek)} {FormatHours(d)}"));
    }

    private static string FormatHours(WorkingHoursDay day, TextLocalizer? t = null) =>
        day.IsClosed ? (t?["Kapalı"] ?? "Kapalı") : $"{Hm(day.OpenTime)} - {Hm(day.CloseTime)}";

    private static string Hm(TimeSpan time) => time.ToString(@"hh\:mm");

    // Müşterinin dilindeki gün adı; Türkçede sabit adlar, diğer dillerde kültürün gün adları kullanılır.
    public static string DayName(DayOfWeek day) =>
        SiteLanguages.Current == SiteLanguages.Default ? TurkishDayName(day) : CultureInfo.CurrentCulture.DateTimeFormat.GetDayName(day);

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
