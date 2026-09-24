using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestoranYonetim.Data;
using RestoranYonetim.Models;
using RestoranYonetim.Security;
using RestoranYonetim.Services;

namespace RestoranYonetim.Controllers;

// Personel sipariş ekranı: taslak siparişler, aktif siparişler, garson çağrıları ve masaya özel adisyon ekranı. Ödeme/adisyon kapatma kasa tarafında (KasaController) yapılır.
[Authorize(Policy = Permissions.Orders.View)]
public class StaffController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly AuditService _audit;
    private readonly AdisyonService _adisyonService;

    public StaffController(ApplicationDbContext context, AuditService audit, AdisyonService adisyonService)
    {
        _context = context;
        _audit = audit;
        _adisyonService = adisyonService;
    }

    public async Task<IActionResult> Index()
    {
        // Sadece garsonun kendi oluşturduğu taslaklar listelenir; müşterinin QR taslak sepeti garson ekranına düşmez.
        var drafts = await _context.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .Include(o => o.Table)
            .Where(o => o.IsDraft && o.Source == OrderSource.Garson)
            .OrderBy(o => o.CreatedAt)
            .ToListAsync();

        var activeOrders = await _context.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .Include(o => o.Table)
            .Where(o => !o.IsDraft && o.Status != OrderStatus.IptalEdildi)
            .Where(o => o.Adisyon == null || (o.Adisyon.Status != AdisyonStatus.Odendi && o.Adisyon.Status != AdisyonStatus.Iptal && o.Adisyon.Status != AdisyonStatus.Iade))
            .OrderBy(o => o.SentAt)
            .ToListAsync();

        ViewBag.Drafts = drafts;

        ViewBag.WaiterCalls = await _context.WaiterCalls
            .AsNoTracking()
            .Include(w => w.Table)
            .Where(w => !w.Resolved)
            .OrderBy(w => w.CreatedAt)
            .ToListAsync();

        ViewBag.RealtimeGroups = "staff";
        return View(activeOrders);
    }

    [HttpPost]
    [Authorize(Policy = Permissions.Orders.Create)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResolveWaiterCall(int id)
    {
        var call = await _context.WaiterCalls.FindAsync(id);
        if (call == null)
        {
            return NotFound();
        }

        call.Resolved = true;
        call.ResolvedAt = DateTime.Now;
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [Authorize(Policy = Permissions.Orders.Create)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendDraft(int id)
    {
        var order = await _context.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id);
        if (order == null || !order.IsDraft)
        {
            return NotFound();
        }

        await _adisyonService.SendOrderAsync(order);
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [Authorize(Policy = Permissions.Orders.Create)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DiscardDraft(int id)
    {
        var order = await _context.Orders.FindAsync(id);
        if (order != null && order.IsDraft)
        {
            _context.Orders.Remove(order);
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [Authorize(Policy = Permissions.Orders.Create)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkServed(int id)
    {
        var order = await _context.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == id);
        if (order == null)
        {
            return NotFound();
        }

        foreach (var item in order.Items.Where(i => i.Status == OrderStatus.Hazir))
        {
            item.Status = OrderStatus.ServisEdildi;
        }
        OrderStatusHelper.Recompute(order);
        await _context.SaveChangesAsync();
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [Authorize(Policy = Permissions.Orders.Create)]
    public async Task<IActionResult> NewOrder()
    {
        ViewBag.Tables = await _context.RestaurantTables.AsNoTracking().OrderBy(t => t.Name).ToListAsync();
        ViewBag.MenuItems = await _context.MenuItems
            .AsNoTracking()
            .Where(m => m.IsAvailable)
            .Include(m => m.Category)
            .OrderBy(m => m.Category!.DisplayOrder)
            .ThenBy(m => m.Name)
            .ToListAsync();
        ViewBag.Options = await _context.MenuItemOptions
            .AsNoTracking()
            .Where(o => o.IsAvailable)
            .ToListAsync();
        return View();
    }

    // Garson yetkili olduğu için MenuController kadar riskli değil, ama fat-finger girişlerine karşı yine de üst sınır var.
    private const int MaxQuantityPerItem = 200;
    private const int MaxNotesLength = 500;

    [HttpPost]
    [Authorize(Policy = Permissions.Orders.Create)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> NewOrder(int tableId, [FromForm] Dictionary<int, int> quantities, List<int>? selectedOptions, string? notes)
    {
        var table = await _context.RestaurantTables.FindAsync(tableId);
        if (table == null)
        {
            TempData["Error"] = "Masa bulunamadı.";
            return RedirectToAction(nameof(NewOrder));
        }

        if (quantities.Any(kv => kv.Value > MaxQuantityPerItem))
        {
            TempData["Error"] = $"Bir üründen en fazla {MaxQuantityPerItem} adet girilebilir.";
            return RedirectToAction(nameof(NewOrder));
        }

        if (!string.IsNullOrEmpty(notes) && notes.Length > MaxNotesLength)
        {
            notes = notes[..MaxNotesLength];
        }

        var order = new Order
        {
            TableId = tableId,
            Source = OrderSource.Garson,
            IsDraft = true,
            Notes = notes,
            CreatedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
            CreatedByUserName = User.Identity?.Name
        };

        var chosenOptionIds = selectedOptions ?? new List<int>();
        var allOptions = await _context.MenuItemOptions.AsNoTracking().Where(o => chosenOptionIds.Contains(o.Id)).ToListAsync();

        // Seçilen ürünler tek sorguda toplu çekilir (N+1 önlemi).
        var orderedQuantities = quantities.Where(kv => kv.Value > 0).ToList();
        var selectedIds = orderedQuantities.Select(kv => kv.Key).ToList();
        var menuItemsById = await _context.MenuItems
            .AsNoTracking()
            .Where(m => selectedIds.Contains(m.Id))
            .ToDictionaryAsync(m => m.Id);

        foreach (var kv in orderedQuantities)
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
            TempData["Error"] = "En az bir ürün seçmelisiniz.";
            return RedirectToAction(nameof(NewOrder));
        }

        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        TempData["Info"] = "Sipariş taslak olarak oluşturuldu. Kontrol edip \"Gönder\" demeyi unutmayın.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Adisyon(int tableId)
    {
        var table = await _context.RestaurantTables.FindAsync(tableId);
        if (table == null)
        {
            return NotFound();
        }

        var acikDurumlar = new[] { AdisyonStatus.Acik, AdisyonStatus.HesapIstendi, AdisyonStatus.Kasada, AdisyonStatus.KismiOdendi };
        var adisyon = await _context.Adisyonlar
            .AsNoTracking()
            .Include(a => a.Orders).ThenInclude(o => o.Items).ThenInclude(i => i.Options)
            .Include(a => a.Payments)
            .Include(a => a.DiscountRequests)
            .Where(a => a.TableId == tableId && acikDurumlar.Contains(a.Status))
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync();

        ViewBag.Table = table;
        await LoadReceiptInfoAsync();
        return View(adisyon);
    }

    private async Task LoadReceiptInfoAsync()
    {
        try
        {
            var content = await _context.SiteContents.AsNoTracking().FirstOrDefaultAsync();
            ViewBag.ReceiptRestaurantName = content?.HeroTitle;
            ViewBag.ReceiptRestaurantAddress = content?.ContactAddress;
            ViewBag.ReceiptRestaurantPhone = content?.ContactPhone;
        }
        catch
        {
            ViewBag.ReceiptRestaurantName = null;
            ViewBag.ReceiptRestaurantAddress = null;
            ViewBag.ReceiptRestaurantPhone = null;
        }
    }

    // Garson adisyonu kasaya gönderir ama doğrudan kapatamaz/ödemeyi onaylayamaz - bu kasa/yönetimin işi.
    [HttpPost]
    [Authorize(Policy = Permissions.Orders.Create)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> KasayaGonder(int adisyonId)
    {
        var adisyon = await _context.Adisyonlar.Include(a => a.Table).FirstOrDefaultAsync(a => a.Id == adisyonId);
        if (adisyon == null)
        {
            return NotFound();
        }

        adisyon.Status = AdisyonStatus.Kasada;
        if (adisyon.Table != null)
        {
            adisyon.Table.Status = TableStatus.HesapIstendi;
        }
        await _context.SaveChangesAsync();

        await _audit.LogAsync(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            User.Identity?.Name,
            "Adisyon kasaya gönderildi",
            entityName: "Adisyon",
            entityId: adisyonId.ToString(),
            details: null);

        return RedirectToAction(nameof(Adisyon), new { tableId = adisyon.TableId });
    }

    [HttpPost]
    [Authorize(Policy = Permissions.Orders.Create)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestDiscount(int adisyonId, decimal amount, string? reason)
    {
        var adisyon = await _context.Adisyonlar
            .Include(a => a.Orders).ThenInclude(o => o.Items).ThenInclude(i => i.Options)
            .FirstOrDefaultAsync(a => a.Id == adisyonId);
        if (adisyon == null)
        {
            return NotFound();
        }

        // Erken/dostça geri bildirim - nihai/kesin kontrol yine KasaController.ApproveDiscount'ta yapılır.
        if (amount > adisyon.ItemsTotal)
        {
            TempData["Error"] = $"İndirim tutarı hesabın toplamını ({adisyon.ItemsTotal:0.00} TL) aşamaz.";
            return RedirectToAction(nameof(Adisyon), new { tableId = adisyon.TableId });
        }

        // ÖNEMLİ: tutar <=0 iken önceden action sessizce hiçbir şey yapmadan dönüyordu - garson "talep oluştu" sanıyor ama DiscountRequest kaydı oluşmuyordu. Artık açık hata mesajı dönüyor.
        if (amount <= 0)
        {
            TempData["Error"] = "Lütfen geçerli (sıfırdan büyük) bir indirim tutarı girin.";
            return RedirectToAction(nameof(Adisyon), new { tableId = adisyon.TableId });
        }

        _context.DiscountRequests.Add(new DiscountRequest
        {
            AdisyonId = adisyonId,
            Amount = amount,
            Reason = reason,
            RequestedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty,
            RequestedByUserName = User.Identity?.Name
        });
        _context.Notifications.Add(new Notification
        {
            Message = $"İndirim talebi: {amount:0.00} TL ({reason})",
            Link = "/kasa",
            RequiredPermission = Permissions.Cash.ApprovePayment
        });
        await _context.SaveChangesAsync();
        TempData["Info"] = "İndirim talebi oluşturuldu, yönetici onayını bekliyor.";

        return RedirectToAction(nameof(Adisyon), new { tableId = adisyon.TableId });
    }
}
