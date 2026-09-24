using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestoranYonetim.Data;
using RestoranYonetim.Models;
using RestoranYonetim.Security;
using RestoranYonetim.Services;

namespace RestoranYonetim.Areas.Admin.Controllers;

// Herkese açık siteden gelen rezervasyon taleplerinin görüntülenmesi ve temel onay/red akışı.
[Area("Admin")]
[Authorize(Policy = Permissions.Reservations.View)]
public class ReservationsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly AuditService _audit;

    public ReservationsController(ApplicationDbContext context, AuditService audit)
    {
        _context = context;
        _audit = audit;
    }

    private const int PageSize = 50;

    public async Task<IActionResult> Index(int page = 1)
    {
        page = Math.Max(1, page);

        var query = _context.Reservations
            .AsNoTracking()
            .OrderByDescending(r => r.Date)
            .ThenByDescending(r => r.Time);

        var totalCount = await query.CountAsync();
        var reservations = await query.Skip((page - 1) * PageSize).Take(PageSize).ToListAsync();

        // Bekleyen sayısı sadece bu sayfadaki değil, tüm talepler arasından hesaplanır.
        ViewBag.PendingCount = await _context.Reservations.CountAsync(r => r.Status == ReservationStatus.Beklemede);
        ViewBag.TotalCount = totalCount;
        ViewBag.Page = page;
        ViewBag.TotalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)PageSize));

        return View(reservations);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Permissions.Reservations.Manage)]
    public async Task<IActionResult> SetStatus(int id, ReservationStatus status)
    {
        var reservation = await _context.Reservations.FindAsync(id);
        if (reservation == null)
        {
            return NotFound();
        }

        reservation.Status = status;
        await _context.SaveChangesAsync();

        await _audit.LogAsync(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            User.Identity?.Name,
            $"Rezervasyon durumu güncellendi: {status}",
            entityName: "Reservation",
            entityId: id.ToString(),
            details: $"{reservation.Name}, {reservation.Date:dd.MM.yyyy} {reservation.Time}");

        return RedirectToAction(nameof(Index));
    }
}
