using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestoranYonetim.Data;
using RestoranYonetim.Models;
using RestoranYonetim.Security;
using RestoranYonetim.Services;

namespace RestoranYonetim.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = Permissions.Tables.Manage)]
public class RestaurantsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly AuditService _audit;

    public RestaurantsController(ApplicationDbContext context, AuditService audit)
    {
        _context = context;
        _audit = audit;
    }

    public async Task<IActionResult> Index()
    {
        var restaurants = await _context.Restaurants
            .AsNoTracking()
            .Include(r => r.Branches)
            .ThenInclude(b => b.Areas)
            .ThenInclude(a => a.Tables)
            .OrderBy(r => r.Name)
            .ToListAsync();

        var restaurantIds = restaurants.Select(r => r.Id).ToList();
        var revenueByRestaurant = await _context.Adisyonlar
            .AsNoTracking()
            .Where(a => a.Status == AdisyonStatus.Odendi && a.ClosedAt != null && restaurantIds.Contains(a.Table!.Area!.Branch!.RestaurantId))
            .GroupBy(a => a.Table!.Area!.Branch!.RestaurantId)
            .Select(g => new { RestaurantId = g.Key, Revenue = g.Sum(a => a.Orders.Where(o => !o.IsDraft && o.Status != OrderStatus.IptalEdildi).SelectMany(o => o.Items.Where(i => i.Status != OrderStatus.IptalEdildi)).Sum(i => i.Quantity * i.UnitPrice)) })
            .ToDictionaryAsync(x => x.RestaurantId, x => x.Revenue);

        ViewBag.RevenueByRestaurant = revenueByRestaurant;
        return View(restaurants);
    }

    public IActionResult Create() => View(new Restaurant());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Restaurant restaurant)
    {
        if (!ModelState.IsValid)
        {
            return View(restaurant);
        }

        _context.Restaurants.Add(restaurant);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var restaurant = await _context.Restaurants.FindAsync(id);
        if (restaurant == null)
        {
            return NotFound();
        }
        return View(restaurant);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Restaurant restaurant)
    {
        if (id != restaurant.Id)
        {
            return NotFound();
        }
        if (!ModelState.IsValid)
        {
            return View(restaurant);
        }

        _context.Update(restaurant);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int id)
    {
        var restaurant = await _context.Restaurants.FindAsync(id);
        if (restaurant == null)
        {
            return NotFound();
        }
        return View(restaurant);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var restaurant = await _context.Restaurants.FindAsync(id);
        if (restaurant != null)
        {
            var restaurantName = restaurant.Name;
            _context.Restaurants.Remove(restaurant);
            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                // Branch -> Restaurant ilişkisi Restrict - restorana bağlı şube varsa veritabanı silmeyi reddeder.
                TempData["Error"] = "Bu restorana bağlı şubeler var, önce onları silin.";
                return RedirectToAction(nameof(Index));
            }

            await _audit.LogAsync(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                User.Identity?.Name,
                "Restoran silindi",
                entityName: "Restaurant",
                entityId: id.ToString(),
                details: restaurantName);
        }
        return RedirectToAction(nameof(Index));
    }
}
