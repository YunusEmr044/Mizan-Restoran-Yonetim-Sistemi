using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RestoranYonetim.Data;
using RestoranYonetim.Models;
using RestoranYonetim.Security;
using RestoranYonetim.Services;

namespace RestoranYonetim.Areas.Admin.Controllers;

// SSS yönetimi - "Site İçeriği" yetkisiyle korunuyor.
[Area("Admin")]
[Authorize(Policy = Permissions.SiteContent.Manage)]
public class FaqController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly AuditService _audit;

    public FaqController(ApplicationDbContext context, IMemoryCache cache, AuditService audit)
    {
        _context = context;
        _cache = cache;
        _audit = audit;
    }

    public async Task<IActionResult> Index()
    {
        var faqs = await _context.Faqs
            .AsNoTracking()
            .OrderBy(f => f.Category)
            .ThenBy(f => f.DisplayOrder)
            .ToListAsync();
        return View(faqs);
    }

    public IActionResult Create() => View(new Faq { IsActive = true });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Faq model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        _context.Faqs.Add(model);
        await _context.SaveChangesAsync();
        SeoCache.InvalidateFaqs(_cache);
        await _audit.LogAsync(User.FindFirstValue(ClaimTypes.NameIdentifier), User.Identity?.Name,
            "SSS eklendi", entityName: "Faq", entityId: model.Id.ToString(), details: model.Question);

        TempData["Info"] = "Soru eklendi.";
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var faq = await _context.Faqs.FindAsync(id);
        if (faq == null)
        {
            return NotFound();
        }
        return View(faq);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Faq model)
    {
        if (id != model.Id || !ModelState.IsValid)
        {
            return View(model);
        }

        _context.Update(model);
        await _context.SaveChangesAsync();
        SeoCache.InvalidateFaqs(_cache);
        await _audit.LogAsync(User.FindFirstValue(ClaimTypes.NameIdentifier), User.Identity?.Name,
            "SSS güncellendi", entityName: "Faq", entityId: id.ToString(), details: model.Question);

        TempData["Info"] = "Soru güncellendi.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var faq = await _context.Faqs.FindAsync(id);
        if (faq != null)
        {
            _context.Faqs.Remove(faq);
            await _context.SaveChangesAsync();
            SeoCache.InvalidateFaqs(_cache);
            await _audit.LogAsync(User.FindFirstValue(ClaimTypes.NameIdentifier), User.Identity?.Name,
                "SSS silindi", entityName: "Faq", entityId: id.ToString(), details: faq.Question);
        }

        TempData["Info"] = "Soru silindi.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var faq = await _context.Faqs.FindAsync(id);
        if (faq != null)
        {
            faq.IsActive = !faq.IsActive;
            await _context.SaveChangesAsync();
            SeoCache.InvalidateFaqs(_cache);
            await _audit.LogAsync(User.FindFirstValue(ClaimTypes.NameIdentifier), User.Identity?.Name,
                faq.IsActive ? "SSS yayına alındı" : "SSS yayından kaldırıldı",
                entityName: "Faq", entityId: id.ToString(), details: faq.Question);
        }

        return RedirectToAction(nameof(Index));
    }
}
