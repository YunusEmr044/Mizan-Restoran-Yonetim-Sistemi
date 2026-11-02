using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestoranYonetim.Data;
using RestoranYonetim.Models;
using RestoranYonetim.Security;
using RestoranYonetim.Services;
using RestoranYonetim.Services.Printing;

namespace RestoranYonetim.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = Permissions.Printers.Manage)]
public class PrintersController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly AuditService _audit;
    private readonly KitchenPrintService _printing;

    public PrintersController(ApplicationDbContext context, AuditService audit, KitchenPrintService printing)
    {
        _context = context;
        _audit = audit;
        _printing = printing;
    }

    public async Task<IActionResult> Index()
    {
        ViewBag.ModuleEnabled = await _printing.IsEnabledAsync();
        var printers = await _context.KitchenPrinters.AsNoTracking().Include(p => p.Branch)
            .OrderBy(p => p.Branch!.Name).ThenBy(p => p.Department).ThenBy(p => p.Name).ToListAsync();
        return View(printers);
    }

    public async Task<IActionResult> Create()
    {
        await LoadBranchesAsync();
        return View("Form", new KitchenPrinter());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Name,BranchId,Department,Connection,Host,Port,PaperWidthMm,IsActive")] KitchenPrinter input)
    {
        if (!await ValidateAsync(input))
        {
            await LoadBranchesAsync();
            return View("Form", input);
        }

        _context.KitchenPrinters.Add(input);
        await _context.SaveChangesAsync();
        await LogAsync("Yazıcı eklendi", input);
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var printer = await _context.KitchenPrinters.FindAsync(id);
        if (printer == null)
        {
            return NotFound();
        }
        await LoadBranchesAsync();
        return View("Form", printer);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Name,BranchId,Department,Connection,Host,Port,PaperWidthMm,IsActive")] KitchenPrinter input)
    {
        var printer = await _context.KitchenPrinters.FindAsync(id);
        if (printer == null)
        {
            return NotFound();
        }
        input.Id = id;
        if (!await ValidateAsync(input))
        {
            await LoadBranchesAsync();
            return View("Form", input);
        }

        printer.Name = input.Name;
        printer.BranchId = input.BranchId;
        printer.Department = input.Department;
        printer.Connection = input.Connection;
        printer.Host = input.Host;
        printer.Port = input.Port;
        printer.PaperWidthMm = input.PaperWidthMm;
        printer.IsActive = input.IsActive;
        await _context.SaveChangesAsync();
        await LogAsync("Yazıcı güncellendi", printer);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var printer = await _context.KitchenPrinters.FindAsync(id);
        if (printer == null)
        {
            return NotFound();
        }
        printer.IsActive = !printer.IsActive;
        await _context.SaveChangesAsync();
        await LogAsync(printer.IsActive ? "Yazıcı aktif edildi" : "Yazıcı pasife alındı", printer);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Test(int id)
    {
        var printer = await _context.KitchenPrinters.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
        if (printer == null)
        {
            return NotFound();
        }
        var error = await _printing.SendTestAsync(printer);
        if (error != null)
        {
            TempData["Error"] = $"{printer.Name}: test fişi gönderilemedi ({error}).";
        }
        else
        {
            TempData["Info"] = printer.Connection == PrinterConnection.Ag
                ? $"{printer.Name}: test fişi yazıcıya gönderildi."
                : $"{printer.Name}: test fişi istasyona gönderildi; istasyon sayfası açık değilse basılmaz.";
        }
        return RedirectToAction(nameof(Index));
    }

    private async Task<bool> ValidateAsync(KitchenPrinter input)
    {
        input.Name = input.Name?.Trim() ?? string.Empty;
        input.Host = input.Connection == PrinterConnection.Ag ? input.Host?.Trim() : null;
        if (KitchenPrinterValidation.Validate(input) is string error)
        {
            ModelState.AddModelError(string.Empty, error);
        }
        if (!await _context.Branches.AnyAsync(b => b.Id == input.BranchId))
        {
            ModelState.AddModelError(nameof(KitchenPrinter.BranchId), "Şube seçin.");
        }
        return ModelState.IsValid;
    }

    private async Task LoadBranchesAsync() =>
        ViewBag.Branches = await _context.Branches.AsNoTracking().OrderBy(b => b.Name).ToListAsync();

    private Task LogAsync(string action, KitchenPrinter printer) =>
        _audit.LogAsync(User.FindFirstValue(ClaimTypes.NameIdentifier), User.Identity?.Name, action,
            entityName: "KitchenPrinter", entityId: printer.Id.ToString(),
            details: $"{printer.Name} · {printer.Department} · {printer.Connection}{(printer.Host == null ? "" : $" {printer.Host}:{printer.Port}")}");
}
