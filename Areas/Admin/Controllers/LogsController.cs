using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestoranYonetim.Data;
using RestoranYonetim.Security;
using RestoranYonetim.Services;

namespace RestoranYonetim.Areas.Admin.Controllers;

// Denetim (audit) kayıtlarını görüntüleme ekranı - sadece Süper Admin erişebiliyor. Otomatik arka plan temizleme yok (bilinçli tercih); eski kayıtlar sadece Temizle/TemizleOnayla ile elle silinebiliyor.
[Area("Admin")]
[Authorize(Roles = RolePermissions.SuperAdmin)]
public class LogsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly AuditService _audit;

    private const int PageSize = 50;

    public LogsController(ApplicationDbContext context, AuditService audit)
    {
        _context = context;
        _audit = audit;
    }

    // userId verilmezse tüm kullanıcıların kayıtları tek akışta gösterilir; verilirse o kullanıcıya daralır.
    public async Task<IActionResult> Index(string? userId, int page = 1)
    {
        page = Math.Max(1, page);

        var query = _context.AuditLogs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(userId))
        {
            query = query.Where(a => a.UserId == userId);
        }

        query = query.OrderByDescending(a => a.CreatedAt);

        var totalCount = await query.CountAsync();
        var logs = await query.Skip((page - 1) * PageSize).Take(PageSize).ToListAsync();

        // Filtre listesi ApplicationUsers yerine AuditLogs'tan türetiliyor - silinmiş bir kullanıcının geçmiş kayıtları da görünmeye devam eder.
        var users = await _context.AuditLogs
            .AsNoTracking()
            .Where(a => a.UserId != null)
            .Select(a => new { a.UserId, a.UserName })
            .Distinct()
            .OrderBy(u => u.UserName)
            .ToListAsync();

        ViewBag.Users = users;
        ViewBag.SelectedUserId = userId;
        ViewBag.TotalCount = totalCount;
        ViewBag.Page = page;
        ViewBag.TotalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)PageSize));

        return View(logs);
    }

    public async Task<IActionResult> Export(string? userId)
    {
        var query = _context.AuditLogs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(userId))
        {
            query = query.Where(a => a.UserId == userId);
        }

        var logs = await query.OrderByDescending(a => a.CreatedAt).ToListAsync();

        var csv = CsvExporter.Build(
            new[] { "Tarih", "Kullanıcı", "İşlem", "Varlık", "Varlık Id", "Detay" },
            logs.Select(l => (IEnumerable<object?>)new object?[]
            {
                l.CreatedAt.ToString("dd.MM.yyyy HH:mm:ss"),
                l.UserName ?? l.UserId,
                l.Action,
                l.EntityName,
                l.EntityId,
                l.Details
            }));

        return File(csv, "text/csv", $"denetim-kayitlari-{DateTime.Now:yyyyMMdd-HHmm}.csv");
    }

    [HttpGet]
    public async Task<IActionResult> Temizle()
    {
        ViewBag.TotalLogs = await _context.AuditLogs.CountAsync();
        ViewBag.TotalNotifications = await _context.Notifications.CountAsync();
        ViewBag.OldestLog = await _context.AuditLogs.OrderBy(a => a.CreatedAt).Select(a => (DateTime?)a.CreatedAt).FirstOrDefaultAsync();
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TemizleOnayla(DateTime? beforeDate, string? confirmText)
    {
        // Kazara gönderime karşı ek bariyer - toplu/tarih aralıklı bir silme olduğu için sadece tarih değil "SİL" yazılı onay da gerekiyor.
        if (!string.Equals(confirmText?.Trim(), "SİL", StringComparison.OrdinalIgnoreCase))
        {
            TempData["Error"] = "Onay metni yanlış yazıldı (\"SİL\" yazmanız gerekiyor) - hiçbir kayıt silinmedi.";
            return RedirectToAction(nameof(Temizle));
        }

        if (!beforeDate.HasValue || beforeDate.Value.Date >= DateTime.Today)
        {
            TempData["Error"] = "Geçerli bir tarih seçin - sadece BUGÜNDEN ÖNCEKİ kayıtlar silinebilir.";
            return RedirectToAction(nameof(Temizle));
        }

        var cutoff = beforeDate.Value.Date;

        // ExecuteDeleteAsync doğrudan SQL DELETE üretir (binlerce satırda performanslı). NotificationRead->Notification FK Cascade olduğundan okunma kayıtları otomatik temizlenir.
        var deletedLogs = await _context.AuditLogs.Where(a => a.CreatedAt < cutoff).ExecuteDeleteAsync();
        var deletedNotifications = await _context.Notifications.Where(n => n.CreatedAt < cutoff).ExecuteDeleteAsync();

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var userName = User.Identity?.Name;
        // Bu log cutoff tarihinden sonra oluşturulduğu için az önce silinen kayıtlar arasına karışıp kendini silmiyor.
        await _audit.LogAsync(userId, userName, "Eski denetim kayıtları/bildirimler elle temizlendi",
            entityName: "AuditLog/Notification",
            details: $"{cutoff:dd.MM.yyyy} tarihinden önceki {deletedLogs} denetim kaydı ve {deletedNotifications} bildirim silindi.");

        TempData["Info"] = $"{cutoff:dd.MM.yyyy} tarihinden önceki {deletedLogs} denetim kaydı ve {deletedNotifications} bildirim silindi.";
        return RedirectToAction(nameof(Index));
    }
}
