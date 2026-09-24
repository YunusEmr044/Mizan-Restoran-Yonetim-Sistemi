using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestoranYonetim.Data;
using RestoranYonetim.Models;
using RestoranYonetim.Security;
using RestoranYonetim.Services;

namespace RestoranYonetim.Areas.Admin.Controllers;

// Müşteri/sadakat listesi. Asıl akış kasada ödeme onayında telefon girilmesiyle otomatik oluşur (bkz. KasaController.ConfirmPayment).
[Area("Admin")]
[Authorize(Policy = Permissions.Customers.View)]
public class CustomersController : Controller
{
    private readonly ApplicationDbContext _context;

    public CustomersController(ApplicationDbContext context)
    {
        _context = context;
    }

    private const int PageSize = 50;

    public async Task<IActionResult> Index(string? q, int page = 1)
    {
        page = Math.Max(1, page);

        var query = _context.Customers.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            query = query.Where(c => c.Name.Contains(q) || c.Phone.Contains(q));
        }
        ViewBag.Query = q;

        query = query.OrderByDescending(c => c.TotalSpent);
        var totalCount = await query.CountAsync();
        var customers = await query.Skip((page - 1) * PageSize).Take(PageSize).ToListAsync();

        ViewBag.TotalCount = totalCount;
        ViewBag.Page = page;
        ViewBag.TotalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)PageSize));

        return View(customers);
    }

    [Authorize(Policy = Permissions.Customers.Manage)]
    public IActionResult Create() => View(new Customer());

    [HttpPost]
    [Authorize(Policy = Permissions.Customers.Manage)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Customer customer)
    {
        if (!ModelState.IsValid)
        {
            return View(customer);
        }

        // ÖNEMLİ: kasadaki eşleştirme telefonu normalize edilmiş haliyle arar - burada da normalize edilmezse aynı müşteri için ikinci bir kayıt oluşabilir.
        customer.Phone = PhoneNormalizer.Normalize(customer.Phone);

        _context.Customers.Add(customer);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Policy = Permissions.Customers.Manage)]
    public async Task<IActionResult> Edit(int id)
    {
        var customer = await _context.Customers.FindAsync(id);
        if (customer == null)
        {
            return NotFound();
        }
        return View(customer);
    }

    [HttpPost]
    [Authorize(Policy = Permissions.Customers.Manage)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Customer customer)
    {
        if (id != customer.Id)
        {
            return NotFound();
        }
        if (!ModelState.IsValid)
        {
            return View(customer);
        }

        // TotalSpent/CreatedAt formdan gelmiyor (kasadan otomatik hesaplanır) - mevcut değer korunur.
        var existing = await _context.Customers.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        if (existing == null)
        {
            return NotFound();
        }
        customer.TotalSpent = existing.TotalSpent;
        customer.CreatedAt = existing.CreatedAt;
        customer.Phone = PhoneNormalizer.Normalize(customer.Phone);

        _context.Update(customer);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }
}
