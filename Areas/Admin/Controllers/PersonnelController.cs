using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestoranYonetim.Data;
using RestoranYonetim.Extensions;
using RestoranYonetim.Models;
using RestoranYonetim.Security;
using RestoranYonetim.Services;

namespace RestoranYonetim.Areas.Admin.Controllers;

public sealed record StaffMember(string Id, string Name);

public sealed record ShiftWeekView(DateTime WeekStart, IReadOnlyList<StaffMember> Staff, IReadOnlyList<StaffShift> Shifts, IReadOnlyList<LeaveRequest> Leaves);

public sealed record TimeView(DateTime From, DateTime To, IReadOnlyList<WorkTotal> Totals, IReadOnlyList<TimeEntry> Entries);

// Personel yönetimi: haftalık vardiya planı, mesai kayıtları ve izin talepleri.
[Area("Admin")]
[Authorize(Policy = Permissions.Staff.Manage)]
public class PersonnelController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AuditService _audit;

    public PersonnelController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, AuditService audit)
    {
        _context = context;
        _userManager = userManager;
        _audit = audit;
    }

    private string? ActorId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    private async Task<IReadOnlyList<StaffMember>> ActiveStaffAsync()
    {
        var users = await _userManager.Users.AsNoTracking().ToListAsync();
        return users.Where(u => !UserActivationRules.IsDeactivated(u.LockoutEnd))
            .Select(u => new StaffMember(u.Id, string.IsNullOrWhiteSpace(u.FullName) ? u.Email ?? u.UserName ?? u.Id : u.FullName))
            .OrderBy(s => s.Name, StringComparer.Create(new System.Globalization.CultureInfo("tr-TR"), true))
            .ToList();
    }

    public async Task<IActionResult> Shifts(DateTime? week)
    {
        var start = PersonnelRules.WeekStart(week ?? DateTime.Today);
        var end = start.AddDays(7);
        var shifts = await _context.StaffShifts.AsNoTracking().Where(s => s.Date >= start && s.Date < end).OrderBy(s => s.Start).ToListAsync();
        var leaves = await _context.LeaveRequests.AsNoTracking()
            .Where(l => l.Status == LeaveStatus.Onaylandi && l.StartDate < end && l.EndDate >= start).ToListAsync();
        return View(new ShiftWeekView(start, await ActiveStaffAsync(), shifts, leaves));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddShift(string userId, DateTime date, TimeSpan start, TimeSpan end, string? note)
    {
        var member = (await ActiveStaffAsync()).FirstOrDefault(s => s.Id == userId);
        if (member == null)
        {
            TempData["Error"] = "Personel seçin.";
            return RedirectToAction(nameof(Shifts), new { week = date.ToString("yyyy-MM-dd") });
        }
        var sameDay = await _context.StaffShifts.AsNoTracking().Where(s => s.UserId == userId && s.Date == date.Date)
            .Select(s => new { s.Start, s.End }).ToListAsync();
        var error = PersonnelRules.ShiftError(start, end, sameDay.Select(s => (s.Start, s.End)));
        if (error != null)
        {
            TempData["Error"] = error;
            return RedirectToAction(nameof(Shifts), new { week = date.ToString("yyyy-MM-dd") });
        }
        _context.StaffShifts.Add(new StaffShift
        {
            UserId = userId, UserName = member.Name, Date = date.Date, Start = start, End = end,
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim()[..Math.Min(note.Trim().Length, 200)]
        });
        await _context.SaveChangesAsync();
        TempData["Info"] = $"{member.Name} için {date:dd.MM} {start:hh\\:mm}-{end:hh\\:mm} vardiyası eklendi.";
        return RedirectToAction(nameof(Shifts), new { week = date.ToString("yyyy-MM-dd") });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteShift(int id)
    {
        var shift = await _context.StaffShifts.FindAsync(id);
        if (shift == null)
        {
            return NotFound();
        }
        _context.StaffShifts.Remove(shift);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Shifts), new { week = shift.Date.ToString("yyyy-MM-dd") });
    }

    // Bir önceki haftanın planını bu haftaya kopyalar; bu haftada zaten vardiyası olan personelin günleri atlanır.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CopyPreviousWeek(DateTime week)
    {
        var start = PersonnelRules.WeekStart(week);
        var previous = await _context.StaffShifts.AsNoTracking().Where(s => s.Date >= start.AddDays(-7) && s.Date < start).ToListAsync();
        var existing = await _context.StaffShifts.AsNoTracking().Where(s => s.Date >= start && s.Date < start.AddDays(7))
            .Select(s => new { s.UserId, s.Date }).ToListAsync();
        var activeIds = (await ActiveStaffAsync()).Select(s => s.Id).ToHashSet();
        var copies = previous.Where(p => activeIds.Contains(p.UserId) && !existing.Any(e => e.UserId == p.UserId && e.Date == p.Date.AddDays(7)))
            .Select(p => new StaffShift { UserId = p.UserId, UserName = p.UserName, Date = p.Date.AddDays(7), Start = p.Start, End = p.End, Note = p.Note })
            .ToList();
        _context.StaffShifts.AddRange(copies);
        await _context.SaveChangesAsync();
        TempData["Info"] = copies.Count == 0 ? "Kopyalanacak vardiya bulunamadı." : $"Geçen haftadan {copies.Count} vardiya kopyalandı.";
        return RedirectToAction(nameof(Shifts), new { week = start.ToString("yyyy-MM-dd") });
    }

    public async Task<IActionResult> Time(DateTime? from, DateTime? to)
    {
        var start = (from ?? PersonnelRules.WeekStart(DateTime.Today)).Date;
        var end = (to ?? start.AddDays(6)).Date;
        if (end < start)
        {
            (start, end) = (end, start);
        }
        var until = end.AddDays(1);
        var entries = await _context.TimeEntries.AsNoTracking()
            .Where(e => e.ClockIn < until && (e.ClockOut == null || e.ClockOut > start))
            .OrderByDescending(e => e.ClockIn).ToListAsync();
        return View(new TimeView(start, end, PersonnelRules.Totals(entries, start, until), entries));
    }

    // Çıkışı unutulan kaydı yönetici kapatır.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CloseEntry(int id, DateTime clockOut)
    {
        var entry = await _context.TimeEntries.FindAsync(id);
        if (entry == null)
        {
            return NotFound();
        }
        if (entry.ClockOut != null || clockOut <= entry.ClockIn || clockOut > DateTime.Now || clockOut - entry.ClockIn > TimeSpan.FromHours(24))
        {
            TempData["Error"] = "Çıkış saati girişten sonra, şimdiden önce ve en fazla 24 saat sonrası olmalı.";
            return RedirectToAction(nameof(Time));
        }
        entry.ClockOut = clockOut;
        entry.Note = "Çıkışı yönetici girdi";
        await _context.SaveChangesAsync();
        await _audit.LogAsync(ActorId, User.Identity?.Name, "Mesai çıkışı elle girildi", entityName: "TimeEntry", entityId: id.ToString(),
            details: $"{entry.UserName}: {entry.ClockIn:dd.MM HH:mm} → {clockOut:dd.MM HH:mm}");
        TempData["Info"] = $"{entry.UserName} kaydının çıkışı girildi.";
        return RedirectToAction(nameof(Time), new { from = entry.ClockIn.ToString("yyyy-MM-dd") });
    }

    public async Task<IActionResult> Leaves()
    {
        var leaves = await _context.LeaveRequests.AsNoTracking()
            .OrderBy(l => l.Status == LeaveStatus.Beklemede ? 0 : 1).ThenByDescending(l => l.StartDate).Take(200).ToListAsync();
        return View(leaves);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DecideLeave(int id, bool approve)
    {
        var leave = await _context.LeaveRequests.FindAsync(id);
        if (leave == null)
        {
            return NotFound();
        }
        if (leave.Status != LeaveStatus.Beklemede)
        {
            TempData["Error"] = "Bu talep zaten sonuçlandırılmış.";
            return RedirectToAction(nameof(Leaves));
        }
        leave.Status = approve ? LeaveStatus.Onaylandi : LeaveStatus.Reddedildi;
        leave.DecidedByUserName = User.Identity?.Name;
        leave.DecidedAt = DateTime.Now;
        await _context.SaveChangesAsync();
        await _audit.LogAsync(ActorId, User.Identity?.Name, approve ? "İzin onaylandı" : "İzin reddedildi", entityName: "LeaveRequest", entityId: id.ToString(),
            details: $"{leave.UserName}: {leave.StartDate:dd.MM.yyyy}-{leave.EndDate:dd.MM.yyyy} ({leave.Type.Label()})");
        TempData["Info"] = $"{leave.UserName} kişisinin izni {(approve ? "onaylandı" : "reddedildi")}.";
        return RedirectToAction(nameof(Leaves));
    }
}
