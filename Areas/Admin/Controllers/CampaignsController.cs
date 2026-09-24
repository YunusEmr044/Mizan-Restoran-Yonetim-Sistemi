using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RestoranYonetim.Data;
using RestoranYonetim.Models;
using RestoranYonetim.Security;
using RestoranYonetim.Services;

namespace RestoranYonetim.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = Permissions.Campaigns.View)]
public class CampaignsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly AuditService _audit;
    private readonly IWebHostEnvironment _env;

    public CampaignsController(ApplicationDbContext context, IMemoryCache cache, AuditService audit, IWebHostEnvironment env)
    {
        _context = context;
        _cache = cache;
        _audit = audit;
        _env = env;
    }

    private async Task<string> SaveImageAsync(IFormFile file, string? nameHint)
    {
        var uploadsDir = Path.Combine(_env.WebRootPath, "uploads", "campaigns");
        Directory.CreateDirectory(uploadsDir);
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        var fileName = SlugHelper.BuildFileName(nameHint, "kampanya", ext);
        var fullPath = Path.Combine(uploadsDir, fileName);
        using (var stream = new FileStream(fullPath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }
        return $"/uploads/campaigns/{fileName}";
    }

    private void DeleteImageFileIfExists(string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return;
        }
        try
        {
            var fullPath = Path.Combine(_env.WebRootPath, imageUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
            if (System.IO.File.Exists(fullPath))
            {
                System.IO.File.Delete(fullPath);
            }
        }
        catch (IOException)
        {
            // Dosya kilitli/erişilemez olabilir - veritabanı işlemi zaten tamamlandı, sessizce geçilir.
        }
    }

    public async Task<IActionResult> Index()
    {
        var campaigns = await _context.Campaigns.AsNoTracking().OrderByDescending(c => c.StartDate).ToListAsync();
        return View(campaigns);
    }

    [Authorize(Policy = Permissions.Campaigns.Manage)]
    public IActionResult Create() => View(new Campaign());

    [HttpPost]
    [Authorize(Policy = Permissions.Campaigns.Manage)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Campaign campaign, IFormFile? imageFile)
    {
        if (!ModelState.IsValid)
        {
            return View(campaign);
        }
        if (imageFile is { Length: > 0 })
        {
            var uploadError = await ImageUploadValidator.ValidateAsync(imageFile);
            if (uploadError != null)
            {
                ModelState.AddModelError(string.Empty, uploadError);
                return View(campaign);
            }
            campaign.ImageUrl = await SaveImageAsync(imageFile, campaign.Name);
        }
        _context.Campaigns.Add(campaign);
        await _context.SaveChangesAsync();
        HomePreviewCache.InvalidateCampaigns(_cache);
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Policy = Permissions.Campaigns.Manage)]
    public async Task<IActionResult> Edit(int id)
    {
        var campaign = await _context.Campaigns.FindAsync(id);
        if (campaign == null)
        {
            return NotFound();
        }
        return View(campaign);
    }

    [HttpPost]
    [Authorize(Policy = Permissions.Campaigns.Manage)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Campaign campaign, IFormFile? imageFile, bool removeImage = false)
    {
        if (id != campaign.Id)
        {
            return NotFound();
        }
        if (!ModelState.IsValid)
        {
            return View(campaign);
        }

        // Veri kaybını önlemek için mevcut ImageUrl DB'deki değerle korunur, sadece yeni dosya/removeImage değiştirir.
        var existing = await _context.Campaigns.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        if (existing == null)
        {
            return NotFound();
        }
        campaign.ImageUrl = existing.ImageUrl;

        if (imageFile is { Length: > 0 })
        {
            var uploadError = await ImageUploadValidator.ValidateAsync(imageFile);
            if (uploadError != null)
            {
                ModelState.AddModelError(string.Empty, uploadError);
                return View(campaign);
            }
            campaign.ImageUrl = await SaveImageAsync(imageFile, campaign.Name);
        }
        else if (removeImage)
        {
            campaign.ImageUrl = null;
        }

        _context.Update(campaign);
        await _context.SaveChangesAsync();
        HomePreviewCache.InvalidateCampaigns(_cache);

        if (existing.ImageUrl != campaign.ImageUrl)
        {
            DeleteImageFileIfExists(existing.ImageUrl);
        }

        return RedirectToAction(nameof(Index));
    }

    [Authorize(Policy = Permissions.Campaigns.Manage)]
    public async Task<IActionResult> Delete(int id)
    {
        var campaign = await _context.Campaigns.FindAsync(id);
        if (campaign == null)
        {
            return NotFound();
        }
        return View(campaign);
    }

    [HttpPost, ActionName("Delete")]
    [Authorize(Policy = Permissions.Campaigns.Manage)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var campaign = await _context.Campaigns.FindAsync(id);
        if (campaign != null)
        {
            var campaignName = campaign.Name;
            var imageUrl = campaign.ImageUrl;
            _context.Campaigns.Remove(campaign);
            await _context.SaveChangesAsync();
            HomePreviewCache.InvalidateCampaigns(_cache);
            DeleteImageFileIfExists(imageUrl);

            await _audit.LogAsync(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                User.Identity?.Name,
                "Kampanya silindi",
                entityName: "Campaign",
                entityId: id.ToString(),
                details: campaignName);
        }
        return RedirectToAction(nameof(Index));
    }
}
