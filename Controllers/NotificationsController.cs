using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestoranYonetim.Data;
using RestoranYonetim.Models;
using RestoranYonetim.Security;

namespace RestoranYonetim.Controllers;

// Bildirim merkezi: kullanıcı, izinlerine göre kendine ait bildirimleri görür. Okunma durumu NotificationRead tablosunda kullanıcı bazında tutulur (paylaşılan tek bir IsRead bayrağı değil).
[Route("bildirimler")]
[Authorize]
public class NotificationsController : Controller
{
    private readonly ApplicationDbContext _context;

    public NotificationsController(ApplicationDbContext context)
    {
        _context = context;
    }

    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

    private IQueryable<Notification> VisibleToCurrentUser()
    {
        var userPermissions = User.Claims
            .Where(c => c.Type == Permissions.ClaimType)
            .Select(c => c.Value)
            .ToHashSet();

        return _context.Notifications
            .Where(n => n.RequiredPermission == null || userPermissions.Contains(n.RequiredPermission));
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var userId = CurrentUserId;

        var notifications = await VisibleToCurrentUser()
            .OrderByDescending(n => n.CreatedAt)
            .Take(100)
            .Select(n => new NotificationListItem
            {
                Notification = n,
                IsRead = _context.NotificationReads.Any(r => r.NotificationId == n.Id && r.UserId == userId)
            })
            .ToListAsync();

        return View(notifications);
    }

    [HttpGet("sayac")]
    public async Task<IActionResult> UnreadCount()
    {
        var userId = CurrentUserId;

        var count = await VisibleToCurrentUser()
            .CountAsync(n => !_context.NotificationReads.Any(r => r.NotificationId == n.Id && r.UserId == userId));

        return Json(new { count });
    }

    [HttpPost("okundu/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkRead(int id)
    {
        var userId = CurrentUserId;

        var alreadyRead = await _context.NotificationReads
            .AnyAsync(r => r.NotificationId == id && r.UserId == userId);

        if (!alreadyRead)
        {
            _context.NotificationReads.Add(new NotificationRead { NotificationId = id, UserId = userId });
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Eşzamanlı çift tıklama unique index'e çarpabilir - sonuç zaten istenen durum olduğundan zararsız no-op.
            }
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("hepsi-okundu")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllRead()
    {
        var userId = CurrentUserId;

        var unreadIds = await VisibleToCurrentUser()
            .Where(n => !_context.NotificationReads.Any(r => r.NotificationId == n.Id && r.UserId == userId))
            .Select(n => n.Id)
            .ToListAsync();

        _context.NotificationReads.AddRange(
            unreadIds.Select(id => new NotificationRead { NotificationId = id, UserId = userId }));

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Eşzamanlı bir istek zaten aynı satırları eklemiş olabilir - zararsız no-op.
        }

        return RedirectToAction(nameof(Index));
    }
}

public class NotificationListItem
{
    public Notification Notification { get; set; } = null!;
    public bool IsRead { get; set; }
}
