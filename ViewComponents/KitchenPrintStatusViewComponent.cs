using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestoranYonetim.Data;
using RestoranYonetim.Models;
using RestoranYonetim.Services.Printing;

namespace RestoranYonetim.ViewComponents;

public record KitchenPrintStatusModel(IReadOnlyList<PrintFailure> Failures, IReadOnlyList<KitchenPrinter> Stations);

// Mutfak/bar ekranının üstünde basılamayan fiş uyarısı ve tarayıcı yazıcısı istasyon bağlantıları.
public class KitchenPrintStatusViewComponent : ViewComponent
{
    private readonly ApplicationDbContext _context;
    private readonly PrintFailureLog _failures;
    private readonly KitchenPrintService _printing;

    public KitchenPrintStatusViewComponent(ApplicationDbContext context, PrintFailureLog failures, KitchenPrintService printing)
    {
        _context = context;
        _failures = failures;
        _printing = printing;
    }

    public async Task<IViewComponentResult> InvokeAsync(MenuDepartment department)
    {
        var stations = await _printing.IsEnabledAsync()
            ? await _context.KitchenPrinters.AsNoTracking().Include(p => p.Branch)
                .Where(p => p.IsActive && p.Department == department && p.Connection == PrinterConnection.Tarayici)
                .OrderBy(p => p.Name).ToListAsync()
            : new List<KitchenPrinter>();
        return View(new KitchenPrintStatusModel(_failures.Recent(department, DateTime.Now), stations));
    }
}
