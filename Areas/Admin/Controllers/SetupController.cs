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

public sealed record SetupView(IReadOnlyList<SetupStep> Steps, bool HasTables);

// Canlıya geçiş kontrol listesi: eksik adımlar ve hiç masa yoksa tek adımda masa oluşturma.
[Area("Admin")]
[Authorize(Policy = Permissions.Settings.Manage)]
public class SetupController : Controller
{
    public const int MaxTables = 200;

    private readonly ApplicationDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly IConfiguration _configuration;
    private readonly AuditService _audit;

    public SetupController(ApplicationDbContext context, IMemoryCache cache, IConfiguration configuration, AuditService audit)
    {
        _context = context;
        _cache = cache;
        _configuration = configuration;
        _audit = audit;
    }

    public async Task<IActionResult> Index()
    {
        var steps = await SetupChecklistLoader.LoadAsync(_context, _cache, _configuration);
        return View(new SetupView(steps, await _context.RestaurantTables.AnyAsync()));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateTables(string? areaName, int count)
    {
        if (count < 1 || count > MaxTables)
        {
            TempData["Error"] = $"Masa sayısı 1 ile {MaxTables} arasında olmalı.";
            return RedirectToAction(nameof(Index));
        }
        if (await _context.RestaurantTables.AnyAsync())
        {
            TempData["Error"] = "Masalar zaten var; yenilerini Masalar / QR sayfasından ekleyin.";
            return RedirectToAction(nameof(Index));
        }

        var area = await _context.Areas.OrderBy(a => a.Id).FirstOrDefaultAsync();
        if (area == null)
        {
            var branch = await _context.Branches.OrderBy(b => b.Id).FirstOrDefaultAsync();
            if (branch == null)
            {
                var content = await SiteContentCache.GetAsync(_context, _cache);
                var restaurant = await _context.Restaurants.OrderBy(r => r.Id).FirstOrDefaultAsync()
                    ?? new Restaurant { Name = SetupChecklist.Filled(content?.HeroTitle) ? content!.HeroTitle : "Restoranım" };
                branch = new Branch { Restaurant = restaurant, Name = "Merkez Şube" };
                _context.Branches.Add(branch);
            }
            var trimmed = areaName?.Trim() ?? string.Empty;
            area = new Area { Branch = branch, Name = trimmed.Length == 0 ? "Salon" : trimmed[..Math.Min(trimmed.Length, 100)] };
            _context.Areas.Add(area);
        }

        foreach (var name in SetupChecklist.NewTableNames(Array.Empty<string>(), count))
        {
            _context.RestaurantTables.Add(new RestaurantTable { Area = area, Name = name, QrToken = Guid.NewGuid().ToString("N") });
        }
        await _context.SaveChangesAsync();
        await _audit.LogAsync(User.FindFirstValue(ClaimTypes.NameIdentifier), User.Identity?.Name, "Kurulumda masalar oluşturuldu",
            entityName: "RestaurantTable", details: $"{count} masa · {area.Name}");
        TempData["Info"] = $"{count} masa oluşturuldu. QR kodlarını Masalar / QR sayfasından yazdırabilirsiniz.";
        return RedirectToAction(nameof(Index));
    }
}
