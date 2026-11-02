using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestoranYonetim.Data;
using RestoranYonetim.Models;
using RestoranYonetim.Services;

namespace RestoranYonetim.Controllers;

public sealed record MyWorkView(TimeEntry? OpenEntry, IReadOnlyList<StaffShift> Shifts, IReadOnlyList<LeaveRequest> Leaves, IReadOnlyList<TimeEntry> Recent, DateTime WeekStart);

// Personelin kendi sayfası: işe başlama/çıkış, bu haftaki vardiyaları ve izin talepleri.
[Authorize]
[Route("hesabim/mesai")]
public class MesaiController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public MesaiController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    private async Task<string> DisplayNameAsync()
    {
        var user = await _userManager.GetUserAsync(User);
        return string.IsNullOrWhiteSpace(user?.FullName) ? User.Identity?.Name ?? UserId : user.FullName;
    }

    private Task<TimeEntry?> OpenEntryAsync() =>
        _context.TimeEntries.Where(e => e.UserId == UserId && e.ClockOut == null).OrderByDescending(e => e.ClockIn).FirstOrDefaultAsync();

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var week = PersonnelRules.WeekStart(DateTime.Today);
        var shifts = await _context.StaffShifts.AsNoTracking().Where(s => s.UserId == UserId && s.Date >= week && s.Date < week.AddDays(14))
            .OrderBy(s => s.Date).ThenBy(s => s.Start).ToListAsync();
        var leaves = await _context.LeaveRequests.AsNoTracking().Where(l => l.UserId == UserId).OrderByDescending(l => l.StartDate).Take(20).ToListAsync();
        var recent = await _context.TimeEntries.AsNoTracking().Where(e => e.UserId == UserId).OrderByDescending(e => e.ClockIn).Take(10).ToListAsync();
        return View(new MyWorkView(await OpenEntryAsync(), shifts, leaves, recent, week));
    }

    [HttpPost("basla")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ClockIn()
    {
        if (await OpenEntryAsync() != null)
        {
            TempData["Error"] = "Zaten işe başladınız; önce çıkış yapın.";
            return RedirectToAction(nameof(Index));
        }
        _context.TimeEntries.Add(new TimeEntry { UserId = UserId, UserName = await DisplayNameAsync(), ClockIn = DateTime.Now });
        await _context.SaveChangesAsync();
        TempData["Info"] = $"İşe başlama saatiniz kaydedildi: {DateTime.Now:HH:mm}";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("cik")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ClockOut()
    {
        var entry = await OpenEntryAsync();
        if (entry == null)
        {
            TempData["Error"] = "Açık bir mesai kaydınız yok.";
            return RedirectToAction(nameof(Index));
        }
        entry.ClockOut = DateTime.Now;
        await _context.SaveChangesAsync();
        TempData["Info"] = $"Çıkış kaydedildi. Bugünkü süre: {(entry.ClockOut.Value - entry.ClockIn).TotalHours:0.##} saat.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("izin")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestLeave(DateTime startDate, DateTime endDate, LeaveType type, string? reason)
    {
        if (startDate.Date < DateTime.Today.AddDays(-30))
        {
            TempData["Error"] = "30 günden eski bir tarih için izin istenemez.";
            return RedirectToAction(nameof(Index));
        }
        var existing = await _context.LeaveRequests.AsNoTracking()
            .Where(l => l.UserId == UserId && (l.Status == LeaveStatus.Beklemede || l.Status == LeaveStatus.Onaylandi))
            .Select(l => new { l.StartDate, l.EndDate }).ToListAsync();
        var error = PersonnelRules.LeaveError(startDate, endDate, existing.Select(l => (l.StartDate, l.EndDate)));
        if (error != null || !Enum.IsDefined(type))
        {
            TempData["Error"] = error ?? "İzin türü seçin.";
            return RedirectToAction(nameof(Index));
        }
        _context.LeaveRequests.Add(new LeaveRequest
        {
            UserId = UserId, UserName = await DisplayNameAsync(), StartDate = startDate.Date, EndDate = endDate.Date, Type = type,
            Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim()[..Math.Min(reason.Trim().Length, 300)]
        });
        await _context.SaveChangesAsync();
        TempData["Info"] = "İzin talebiniz yöneticiye iletildi.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("izin/{id:int}/geri-cek")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> WithdrawLeave(int id)
    {
        var leave = await _context.LeaveRequests.FirstOrDefaultAsync(l => l.Id == id && l.UserId == UserId);
        if (leave == null)
        {
            return NotFound();
        }
        if (leave.Status != LeaveStatus.Beklemede)
        {
            TempData["Error"] = "Sonuçlanmış bir talep geri çekilemez.";
            return RedirectToAction(nameof(Index));
        }
        leave.Status = LeaveStatus.GeriCekildi;
        await _context.SaveChangesAsync();
        TempData["Info"] = "İzin talebiniz geri çekildi.";
        return RedirectToAction(nameof(Index));
    }
}
