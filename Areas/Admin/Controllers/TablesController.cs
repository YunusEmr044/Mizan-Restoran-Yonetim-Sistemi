using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using QRCoder;
using RestoranYonetim.Data;
using RestoranYonetim.Models;
using RestoranYonetim.Security;
using RestoranYonetim.Services;

namespace RestoranYonetim.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = Permissions.Tables.Manage)]
public class TablesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly AuditService _audit;
    private readonly IMemoryCache _cache;

    public TablesController(ApplicationDbContext context, IConfiguration configuration, AuditService audit, IMemoryCache cache)
    {
        _context = context;
        _configuration = configuration;
        _audit = audit;
        _cache = cache;
    }

    public async Task<IActionResult> Index()
    {
        var tables = await _context.RestaurantTables
            .AsNoTracking()
            .Include(t => t.Area)
            .ThenInclude(a => a!.Branch)
            .ThenInclude(b => b!.Restaurant)
            .OrderBy(t => t.Name)
            .ToListAsync();

        var (qrBaseUrl, qrBaseSource) = await PublicUrl.ResolveWithSourceAsync(HttpContext, _context, _cache, _configuration);
        ViewBag.QrBaseUrl = qrBaseUrl;
        ViewBag.QrBaseSource = qrBaseSource;
        return View(tables);
    }

    private async Task PopulateAreasAsync()
    {
        ViewBag.Areas = await _context.Areas
            .AsNoTracking()
            .Include(a => a.Branch)
            .ThenInclude(b => b!.Restaurant)
            .OrderBy(a => a.Name)
            .ToListAsync();
    }

    public async Task<IActionResult> Create()
    {
        await PopulateAreasAsync();
        return View(new RestaurantTable());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(RestaurantTable table)
    {
        if (!ModelState.IsValid)
        {
            await PopulateAreasAsync();
            return View(table);
        }

        table.QrToken = Guid.NewGuid().ToString("N");
        _context.RestaurantTables.Add(table);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var table = await _context.RestaurantTables.FindAsync(id);
        if (table == null)
        {
            return NotFound();
        }
        await PopulateAreasAsync();
        return View(table);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, RestaurantTable table)
    {
        if (id != table.Id)
        {
            return NotFound();
        }
        if (!ModelState.IsValid)
        {
            await PopulateAreasAsync();
            return View(table);
        }

        // QR token formdan değil, sadece "Yenile" aksiyonuyla değiştirilir.
        var existing = await _context.RestaurantTables.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id);
        if (existing != null)
        {
            table.QrToken = existing.QrToken;
        }

        _context.Update(table);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int id)
    {
        var table = await _context.RestaurantTables
            .AsNoTracking()
            .Include(t => t.Area)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (table == null)
        {
            return NotFound();
        }
        return View(table);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var table = await _context.RestaurantTables.FindAsync(id);
        if (table != null)
        {
            var tableName = table.Name;
            _context.RestaurantTables.Remove(table);
            await _context.SaveChangesAsync();

            await _audit.LogAsync(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                User.Identity?.Name,
                "Masa silindi",
                entityName: "RestaurantTable",
                entityId: id.ToString(),
                details: tableName);
        }
        return RedirectToAction(nameof(Index));
    }

    // Eski token artık hiçbir masayı işaret etmediği için önceden basılmış/paylaşılmış eski QR kodları geçersiz kalır.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RenewQr(int id)
    {
        var table = await _context.RestaurantTables.FindAsync(id);
        if (table == null)
        {
            return NotFound();
        }

        table.QrToken = Guid.NewGuid().ToString("N");
        await _context.SaveChangesAsync();

        await _audit.LogAsync(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            User.Identity?.Name,
            "Masa QR kodu yenilendi",
            entityName: "RestaurantTable",
            entityId: id.ToString(),
            details: table.Name);

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> QrImage(int id)
    {
        var table = await _context.RestaurantTables.FindAsync(id);
        if (table == null)
        {
            return NotFound();
        }

        // GÜVENLİK: Request.Host sahtelenirse QR'a kontrolsüz bir alan adı gömülebilir (Host header injection) - bkz. PublicUrl.
        var baseUrl = await PublicUrl.ResolveAsync(HttpContext, _context, _cache, _configuration);
        var url = $"{baseUrl}/menu/{table.QrToken}";

        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
        var pngQr = new PngByteQRCode(data);
        var bytes = pngQr.GetGraphic(20);

        return File(bytes, "image/png");
    }
}
