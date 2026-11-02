using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestoranYonetim.Data;
using RestoranYonetim.Models;
using RestoranYonetim.Security;
using RestoranYonetim.Services.Printing;

namespace RestoranYonetim.Controllers;

// Tarayıcı yazıcısı istasyonu: mutfak/bar cihazında açık durur, gelen fişi basar ve onaylar.
[Authorize]
[Route("Yazici")]
public class YaziciController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly BrowserPrintTracker _tracker;

    public YaziciController(ApplicationDbContext context, BrowserPrintTracker tracker)
    {
        _context = context;
        _tracker = tracker;
    }

    [HttpGet("Istasyon/{id:int}")]
    public async Task<IActionResult> Istasyon(int id)
    {
        var printer = await _context.KitchenPrinters.AsNoTracking().Include(p => p.Branch).FirstOrDefaultAsync(p => p.Id == id);
        if (printer == null || printer.Connection != PrinterConnection.Tarayici)
        {
            return NotFound();
        }
        if (!CanUse(printer.Department))
        {
            return Forbid();
        }

        ViewBag.RealtimeGroups = KitchenPrintService.StationGroup(printer.Id);
        return View(printer);
    }

    [HttpGet("Fis")]
    public IActionResult Fis(Guid token)
    {
        var pending = _tracker.Get(token);
        if (pending == null)
        {
            return NotFound();
        }
        return CanUse(pending.Department) ? View("KitchenTicketPrint", pending) : Forbid();
    }

    [HttpPost("Basildi")]
    [ValidateAntiForgeryToken]
    public IActionResult Basildi(Guid token)
    {
        var pending = _tracker.Get(token);
        if (pending == null)
        {
            return NoContent();
        }
        if (!CanUse(pending.Department))
        {
            return Forbid();
        }
        _tracker.Acknowledge(token);
        return NoContent();
    }

    private bool CanUse(MenuDepartment department) => User.HasClaim(Permissions.ClaimType, PrintFailure.ScreenPermission(department));
}
