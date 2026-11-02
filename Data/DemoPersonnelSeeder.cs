using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RestoranYonetim.Models;
using RestoranYonetim.Services;

namespace RestoranYonetim.Data;

// Yalnızca Development: örnek hesaplar için bu ve gelecek haftanın vardiyaları, geçmiş günlerin mesai kayıtları ve iki izin talebi.
public static class DemoPersonnelSeeder
{
    private sealed record Pattern(string Email, TimeSpan Start, TimeSpan End, int[] Days, string? Note);

    private static readonly Pattern[] Patterns =
    {
        new("mudur@restoran.local", new(9, 0, 0), new(17, 0, 0), new[] { 0, 1, 2, 3, 4 }, null),
        new("garson@restoran.local", new(11, 0, 0), new(19, 0, 0), new[] { 0, 1, 2, 3, 4, 5 }, "Salon"),
        new("kasiyer@restoran.local", new(12, 0, 0), new(22, 0, 0), new[] { 1, 2, 3, 4, 5, 6 }, null),
        new("mutfak@restoran.local", new(10, 0, 0), new(18, 0, 0), new[] { 0, 1, 2, 3, 4 }, null),
        new("bar@restoran.local", new(18, 0, 0), new(2, 0, 0), new[] { 3, 4, 5, 6 }, "Bar")
    };

    public static async Task SeedAsync(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        if (await context.StaffShifts.AnyAsync() || await context.TimeEntries.AnyAsync())
        {
            return;
        }
        var thisWeek = PersonnelRules.WeekStart(DateTime.Today);
        var now = DateTime.Now;
        foreach (var p in Patterns)
        {
            var user = await userManager.FindByEmailAsync(p.Email);
            if (user == null)
            {
                continue;
            }
            var name = string.IsNullOrWhiteSpace(user.FullName) ? p.Email : user.FullName;
            foreach (var week in new[] { thisWeek.AddDays(-7), thisWeek, thisWeek.AddDays(7) })
            {
                foreach (var day in p.Days)
                {
                    var date = week.AddDays(day);
                    if (week >= thisWeek)
                    {
                        context.StaffShifts.Add(new StaffShift { UserId = user.Id, UserName = name, Date = date, Start = p.Start, End = p.End, Note = p.Note });
                    }
                    var clockIn = date + p.Start + TimeSpan.FromMinutes((day * 7 % 11) - 5);
                    var clockOut = date + p.Start + PersonnelRules.ShiftLength(p.Start, p.End) + TimeSpan.FromMinutes(day * 5 % 13);
                    if (clockOut < now)
                    {
                        context.TimeEntries.Add(new TimeEntry { UserId = user.Id, UserName = name, ClockIn = clockIn, ClockOut = clockOut });
                    }
                }
            }
        }

        var kasiyer = await userManager.FindByEmailAsync("kasiyer@restoran.local");
        var bar = await userManager.FindByEmailAsync("bar@restoran.local");
        if (kasiyer != null)
        {
            context.LeaveRequests.Add(new LeaveRequest
            {
                UserId = kasiyer.Id, UserName = kasiyer.FullName ?? kasiyer.Email!, Type = LeaveType.Yillik, Reason = "Aile ziyareti",
                StartDate = thisWeek.AddDays(9), EndDate = thisWeek.AddDays(10)
            });
        }
        if (bar != null)
        {
            context.LeaveRequests.Add(new LeaveRequest
            {
                UserId = bar.Id, UserName = bar.FullName ?? bar.Email!, Type = LeaveType.Hastalik, Status = LeaveStatus.Onaylandi,
                StartDate = thisWeek.AddDays(13), EndDate = thisWeek.AddDays(13), DecidedByUserName = "mudur@restoran.local", DecidedAt = DateTime.Today.AddDays(-1)
            });
        }
        await context.SaveChangesAsync();
    }
}
