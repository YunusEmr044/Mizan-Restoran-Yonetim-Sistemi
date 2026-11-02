using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RestoranYonetim.Data;
using RestoranYonetim.Models;
using RestoranYonetim.Security;
using RestoranYonetim.Services;
using RestoranYonetim.Services.Publishing;

namespace RestoranYonetim.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = Permissions.SiteContent.Manage)]
public class LegalPagesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly IConfiguration _configuration;
    private readonly AuditService _audit;

    public LegalPagesController(ApplicationDbContext context, IMemoryCache cache, IConfiguration configuration, AuditService audit)
    {
        _context = context;
        _cache = cache;
        _configuration = configuration;
        _audit = audit;
    }

    public async Task<IActionResult> Index()
    {
        return View(await _context.LegalPages.AsNoTracking().OrderBy(p => p.Id).ToListAsync());
    }

    public async Task<IActionResult> Edit(int id)
    {
        var page = await _context.LegalPages.FindAsync(id);
        if (page == null)
        {
            return NotFound();
        }
        return View(page);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Title,Body,IsPublished")] LegalPage model)
    {
        var page = await _context.LegalPages.FindAsync(id);
        if (page == null)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            model.Id = page.Id;
            model.Slug = page.Slug;
            model.UpdatedAt = page.UpdatedAt;
            return View(model);
        }

        page.Title = model.Title.Trim();
        page.Body = model.Body;
        page.IsPublished = model.IsPublished;
        page.UpdatedAt = DateTime.Now;
        await _context.SaveChangesAsync();
        LegalPageCache.Invalidate(_cache);

        await _audit.LogAsync(User.FindFirstValue(ClaimTypes.NameIdentifier), User.Identity?.Name,
            "Yasal sayfa güncellendi", entityName: "LegalPage", entityId: id.ToString(), details: page.Title);

        TempData["Info"] = $"\"{page.Title}\" kaydedildi.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Preview(string? body)
    {
        var content = await SiteContentCache.GetAsync(_context, _cache) ?? new SiteContent();
        var baseUrl = await PublicUrl.ResolveAsync(HttpContext, _context, _cache, _configuration);
        return Content(MarkdownRenderer.ToSafeHtml(LegalTemplate.Fill(body ?? string.Empty, LegalTemplateValues.From(content, baseUrl))), "text/html");
    }
}
