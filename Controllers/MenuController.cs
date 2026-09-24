using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RestoranYonetim.Data;
using RestoranYonetim.Models;
using RestoranYonetim.Security;
using RestoranYonetim.Services;

namespace RestoranYonetim.Controllers;

// Müşteri tarafı: masadaki QR kodu okutunca buraya düşer, giriş gerekmez. Sipariş önce taslak oluşturulur, onaylanınca mutfak/bar ekranına düşer.
public class MenuController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly AdisyonService _adisyonService;
    private readonly ILogger<MenuController> _logger;
    private readonly RealtimeNotifier _realtime;
    private readonly IMemoryCache _cache;

    public MenuController(ApplicationDbContext context, AdisyonService adisyonService, ILogger<MenuController> logger, RealtimeNotifier realtime, IMemoryCache cache)
    {
        _context = context;
        _adisyonService = adisyonService;
        _logger = logger;
        _realtime = realtime;
        _cache = cache;
    }

    private async Task ApplyThemeAsync()
    {
        var content = await SiteContentCache.GetAsync(_context, _cache, _logger);
        ThemeCatalog.ApplyToViewBag(ViewBag, content ?? new SiteContent());
    }

    [HttpGet("/menu/{token}")]
    public async Task<IActionResult> Index(string token)
    {
        var table = await _context.RestaurantTables
            .AsNoTracking()
            .Include(t => t.Area)
            .ThenInclude(a => a!.Branch)
            .ThenInclude(b => b!.Restaurant)
            .FirstOrDefaultAsync(t => t.QrToken == token);
        if (table == null)
        {
            return NotFound("Bu QR koduna ait bir masa bulunamadı.");
        }

        var menu = await MenuCache.GetAsync(_context, _cache);
        var categories = menu.Categories;
        ViewBag.OptionsByItem = menu.Options;

        ViewBag.Table = table;
        await ApplyThemeAsync();
        return View(categories);
    }

    // ÖNEMLİ: bu action kimlik doğrulaması gerektirmiyor, quantities ham POST verisi - üst sınır olmadan stok bütünlüğünü bozacak/DoS'a açık miktarlar gönderilebilirdi.
    private const int MaxQuantityPerItem = 50;
    private const int MaxNotesLength = 500;

    [HttpPost("/menu/{token}/siparis")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("public-endpoints")]
    public async Task<IActionResult> PlaceOrder(string token, [FromForm] Dictionary<int, int> quantities, List<int>? selectedOptions, string? notes)
    {
        var table = await _context.RestaurantTables.AsNoTracking().FirstOrDefaultAsync(t => t.QrToken == token);
        if (table == null)
        {
            return NotFound();
        }

        var selected = quantities.Where(kv => kv.Value > 0).ToList();
        if (!selected.Any())
        {
            TempData["Error"] = "Lütfen en az bir ürün seçin.";
            return RedirectToAction("Index", new { token });
        }

        if (selected.Any(kv => kv.Value > MaxQuantityPerItem))
        {
            TempData["Error"] = $"Bir üründen en fazla {MaxQuantityPerItem} adet sipariş verebilirsiniz. Daha fazlası için lütfen garsonumuzla iletişime geçin.";
            return RedirectToAction("Index", new { token });
        }

        if (!string.IsNullOrEmpty(notes) && notes.Length > MaxNotesLength)
        {
            notes = notes[..MaxNotesLength];
        }

        var order = new Order { TableId = table.Id, Source = OrderSource.Musteri, IsDraft = true, Notes = notes };

        var chosenOptionIds = selectedOptions ?? new List<int>();
        var allOptions = await _context.MenuItemOptions.AsNoTracking().Where(o => chosenOptionIds.Contains(o.Id)).ToListAsync();

        // Seçilen ürünler tek sorguda toplu çekilir (N+1 önlemi).
        var selectedIds = selected.Select(kv => kv.Key).ToList();
        var menuItemsById = await _context.MenuItems
            .AsNoTracking()
            .Where(m => selectedIds.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id);

        foreach (var kv in selected)
        {
            if (!menuItemsById.TryGetValue(kv.Key, out var item))
            {
                continue;
            }

            var orderItem = new OrderItem
            {
                MenuItemId = item.Id,
                MenuItemName = item.Name,
                UnitPrice = item.Price,
                Quantity = kv.Value
            };

            foreach (var opt in allOptions.Where(o => o.MenuItemId == item.Id))
            {
                orderItem.Options.Add(new OrderItemOption { Name = opt.Name, ExtraPrice = opt.ExtraPrice });
            }

            order.Items.Add(orderItem);
        }

        if (!order.Items.Any())
        {
            TempData["Error"] = "Lütfen en az bir ürün seçin.";
            return RedirectToAction("Index", new { token });
        }

        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        return RedirectToAction("Sepet", new { token, id = order.Id });
    }

    [HttpGet("/menu/{token}/sepet/{id:int}")]
    public async Task<IActionResult> Sepet(string token, int id)
    {
        var order = await _context.Orders
            .AsNoTracking()
            .Include(o => o.Items).ThenInclude(i => i.Options)
            .Include(o => o.Table)
            .FirstOrDefaultAsync(o => o.Id == id && o.Table!.QrToken == token && o.IsDraft);

        if (order == null)
        {
            return NotFound();
        }

        await ApplyThemeAsync();
        return View(order);
    }

    [HttpPost("/menu/{token}/sepet/{id:int}/onayla")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("public-endpoints")]
    public async Task<IActionResult> OnaylaVeGonder(string token, int id)
    {
        var order = await _context.Orders
            .Include(o => o.Items)
            .Include(o => o.Table)
            .FirstOrDefaultAsync(o => o.Id == id && o.Table!.QrToken == token && o.IsDraft);

        if (order == null)
        {
            return NotFound();
        }

        await _adisyonService.SendOrderAsync(order);
        return RedirectToAction("Confirmation", new { token, id = order.Id });
    }

    [HttpPost("/menu/{token}/garson")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("public-endpoints")]
    public async Task<IActionResult> CallWaiter(string token)
    {
        var table = await _context.RestaurantTables.AsNoTracking().FirstOrDefaultAsync(t => t.QrToken == token);
        if (table == null)
        {
            return NotFound();
        }

        var alreadyActive = await _context.WaiterCalls.AnyAsync(w => w.TableId == table.Id && !w.Resolved);
        if (!alreadyActive)
        {
            _context.WaiterCalls.Add(new WaiterCall { TableId = table.Id });
            _context.Notifications.Add(new Notification
            {
                Message = $"{table.Name} garson çağırdı",
                Link = "/Staff",
                RequiredPermission = Permissions.Orders.View
            });
            await _context.SaveChangesAsync();
            await _realtime.NotifyManyAsync(new[] { "notifications", "staff" }, "waiter-call", $"{table.Name} garson çağırdı");
        }

        TempData["Info"] = "Garson çağrıldı, birazdan masanıza gelecek.";
        return RedirectToAction("Index", new { token });
    }

    [HttpPost("/menu/{token}/hesap-iste")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("public-endpoints")]
    public async Task<IActionResult> HesapIste(string token)
    {
        var table = await _context.RestaurantTables.FirstOrDefaultAsync(t => t.QrToken == token);
        if (table == null)
        {
            return NotFound();
        }

        var acikDurumlar = new[] { AdisyonStatus.Acik, AdisyonStatus.HesapIstendi };
        var adisyon = await _context.Adisyonlar
            .Where(a => a.TableId == table.Id && acikDurumlar.Contains(a.Status))
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync();

        if (adisyon != null)
        {
            adisyon.Status = AdisyonStatus.HesapIstendi;
            if (table.Status != TableStatus.HesapIstendi)
            {
                table.Status = TableStatus.HesapIstendi;
            }
            _context.Notifications.Add(new Notification
            {
                Message = $"{table.Name} hesap istedi",
                Link = "/Staff",
                RequiredPermission = Permissions.Orders.View
            });
            await _context.SaveChangesAsync();
            await _realtime.NotifyManyAsync(new[] { "notifications", "staff" }, "bill-request", $"{table.Name} hesap istedi");
            TempData["Info"] = "Hesap isteğiniz iletildi, garsonumuz birazdan gelecek.";
        }
        else
        {
            TempData["Error"] = "Henüz aktif bir siparişiniz görünmüyor.";
        }

        return RedirectToAction("Index", new { token });
    }

    [HttpGet("/menu/{token}/onay/{id:int}")]
    public async Task<IActionResult> Confirmation(string token, int id)
    {
        var order = await _context.Orders
            .AsNoTracking()
            .Include(o => o.Items).ThenInclude(i => i.Options)
            .Include(o => o.Table)
            .FirstOrDefaultAsync(o => o.Id == id && o.Table!.QrToken == token);

        if (order == null)
        {
            return NotFound();
        }

        await ApplyThemeAsync();
        return View(order);
    }
}
