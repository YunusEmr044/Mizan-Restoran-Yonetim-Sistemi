using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RestoranYonetim.Data;
using RestoranYonetim.Security;
using RestoranYonetim.Services;

namespace RestoranYonetim.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = Permissions.Feedback.View)]
public class FeedbackController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly AuditService _audit;
    private readonly IMemoryCache _cache;

    public FeedbackController(ApplicationDbContext context, AuditService audit, IMemoryCache cache)
    {
        _context = context;
        _audit = audit;
        _cache = cache;
    }

    public async Task<IActionResult> Index()
    {
        var feedbacks = await _context.Feedbacks
            .AsNoTracking()
            .Include(f => f.Table)
            .OrderByDescending(f => f.CreatedAt)
            .Take(200)
            .ToListAsync();

        ViewBag.AverageRating = feedbacks.Any() ? feedbacks.Average(f => f.Rating) : 0;
        return View(feedbacks);
    }

    // Form girişsiz/herkese açık olduğundan yorumlar admin onayı olmadan yayına yansımaz - moderasyonsuz otomatik yayın spam riski taşırdı.
    [HttpPost]
    [Authorize(Policy = Permissions.Feedback.Manage)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TogglePublish(int id)
    {
        var feedback = await _context.Feedbacks.FindAsync(id);
        if (feedback == null)
        {
            return NotFound();
        }

        feedback.IsPublished = !feedback.IsPublished;
        await _context.SaveChangesAsync();
        HomePreviewCache.InvalidateTestimonials(_cache);

        await _audit.LogAsync(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            User.Identity?.Name,
            feedback.IsPublished ? "Geri bildirim yayınlandı (site)" : "Geri bildirim yayından kaldırıldı",
            entityName: "Feedback",
            entityId: id.ToString(),
            details: feedback.Name);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [Authorize(Policy = Permissions.Feedback.Manage)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetHomeOrder(int id, int? homeOrder)
    {
        var feedback = await _context.Feedbacks.FindAsync(id);
        if (feedback == null)
        {
            return NotFound();
        }

        feedback.HomeDisplayOrder = homeOrder;
        await _context.SaveChangesAsync();
        HomePreviewCache.InvalidateTestimonials(_cache);

        return RedirectToAction(nameof(Index));
    }
}
