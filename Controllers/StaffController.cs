using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestoranYonetim.Data;
using RestoranYonetim.Extensions;
using RestoranYonetim.Models;
using RestoranYonetim.Security;
using RestoranYonetim.Services;
using RestoranYonetim.Services.Fiscal;

namespace RestoranYonetim.Controllers;

// Personel sipariş ekranı: taslak siparişler, aktif siparişler, garson çağrıları ve masaya özel adisyon ekranı. Ödeme/adisyon kapatma kasa tarafında (KasaController) yapılır.
[Authorize(Policy = Permissions.Orders.View)]
public class StaffController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly AuditService _audit;
    private readonly AdisyonService _adisyonService;
    private readonly VoidRequestService _voids;
    private readonly TableTransferService _transfers;

    public StaffController(ApplicationDbContext context, AuditService audit, AdisyonService adisyonService, VoidRequestService voids, TableTransferService transfers)
    {
        _voids = voids;
        _transfers = transfers;
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
            .Include(o => o.Adisyon)
            .Where(o => o.IsDraft && o.Source == OrderSource.Garson)
            .OrderBy(o => o.CreatedAt)
            .ToListAsync();

        var activeOrders = await _context.Orders
            .AsNoTracking()
            .Include(o => o.Items)
            .Include(o => o.Table)
            .Include(o => o.Adisyon)
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
        await SetPackageButtonsAsync();
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
    public async Task<IActionResult> NewOrder(int? adisyonId)
    {
        if (adisyonId != null)
        {
            var package = await OpenPackageAsync(adisyonId.Value);
            if (package == null)
            {
                TempData["Error"] = "Paket bulunamadı ya da kapanmış.";
                return RedirectToAction(nameof(Index));
            }
            ViewBag.Package = package;
        }
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
    public async Task<IActionResult> NewOrder(int? tableId, int? adisyonId, [FromForm] Dictionary<int, int>? quantities, List<int>? selectedOptions, string? notes, List<int>? takeawayItems)
    {
        quantities ??= new();
        var package = adisyonId == null ? null : await OpenPackageAsync(adisyonId.Value);
        if (adisyonId != null && package == null)
        {
            TempData["Error"] = "Paket bulunamadı ya da kapanmış.";
            return RedirectToAction(nameof(Index));
        }
        if (package == null && (tableId == null || await _context.RestaurantTables.FindAsync(tableId.Value) == null))
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
            TableId = package == null ? tableId : null,
            AdisyonId = package?.Id,
            Source = OrderSource.Garson,
            IsDraft = true,
            Notes = notes,
            CreatedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
            CreatedByUserName = User.Identity?.Name
        };

        var chosenOptionIds = selectedOptions ?? new List<int>();
        var allOptions = await _context.MenuItemOptions.AsNoTracking().Where(o => o.IsAvailable && chosenOptionIds.Contains(o.Id)).ToListAsync();

        // Seçilen ürünler tek sorguda toplu çekilir (N+1 önlemi).
        var orderedQuantities = quantities.Where(kv => kv.Value > 0).ToList();
        var selectedIds = orderedQuantities.Select(kv => kv.Key).ToList();
        var menuItemsById = await _context.MenuItems
            .AsNoTracking()
            .Include(m => m.Category)
            .Where(m => m.IsAvailable && selectedIds.Contains(m.Id))
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
                VatRate = VatRates.For(item),
                Quantity = kv.Value,
                IsTakeaway = package != null || (takeawayItems?.Contains(item.Id) ?? false)
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
            return RedirectToAction(nameof(NewOrder), new { adisyonId });
        }

        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        TempData["Info"] = "Sipariş taslak olarak oluşturuldu. Kontrol edip \"Gönder\" demeyi unutmayın.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [Authorize(Policy = Permissions.Orders.Create)]
    public async Task<IActionResult> YeniPaket(OrderType tur = OrderType.GelAl)
    {
        if (!await CanCreatePackageAsync(tur))
        {
            return Forbid();
        }
        ViewBag.Type = tur;
        ViewBag.Branches = await _context.Branches.AsNoTracking().OrderBy(b => b.Name).ToListAsync();
        return View(new PackageInfo());
    }

    [HttpPost]
    [Authorize(Policy = Permissions.Orders.Create)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> YeniPaket(OrderType tur, [Bind("CustomerName,CustomerPhone,Address,Note")] PackageInfo input, string? promisedTime, int? branchId)
    {
        if (!await CanCreatePackageAsync(tur))
        {
            return Forbid();
        }

        var branches = await _context.Branches.AsNoTracking().Select(b => b.Id).ToListAsync();
        var branch = branches.Count == 1 ? branches[0] : branchId;
        var package = new PackageInfo
        {
            CustomerName = input.CustomerName?.Trim(),
            CustomerPhone = string.IsNullOrWhiteSpace(input.CustomerPhone) ? null : PhoneNormalizer.Normalize(input.CustomerPhone),
            Address = tur == OrderType.AdreseTeslim ? input.Address?.Trim() : null,
            Note = string.IsNullOrWhiteSpace(input.Note) ? null : input.Note.Trim(),
            PromisedAt = PackageValidation.PromisedAt(promisedTime, DateTime.Now)
        };
        var error = PackageValidation.Validate(tur, package) ?? (branch == null || !branches.Contains(branch.Value) ? "Şube seçin." : null);
        if (error != null)
        {
            TempData["Error"] = error;
            ViewBag.Type = tur;
            ViewBag.Branches = await _context.Branches.AsNoTracking().OrderBy(b => b.Name).ToListAsync();
            return View(input);
        }

        var adisyon = await _adisyonService.CreatePackageAsync(tur, package, branch!.Value);
        await _audit.LogAsync(User.FindFirstValue(ClaimTypes.NameIdentifier), User.Identity?.Name, "Paket oluşturuldu",
            entityName: "Adisyon", entityId: adisyon.Id.ToString(), details: AdisyonLabel.For(adisyon));
        return RedirectToAction(nameof(NewOrder), new { adisyonId = adisyon.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Paketler()
    {
        var today = DateTime.Today;
        var packages = await _context.Adisyonlar
            .AsNoTracking()
            .IncludeTotals()
            .Where(a => a.OrderType != OrderType.Masa
                && (Models.Adisyon.ActiveStatuses.Contains(a.Status)
                    || (a.Status == AdisyonStatus.Odendi && a.CreatedAt >= today && a.Package!.HandedOverAt == null && a.Package.DeliveredAt == null)))
            .ToListAsync();
        await SetPackageButtonsAsync();
        return View(packages.OrderBy(a => a.Package?.PromisedAt ?? a.CreatedAt).ToList());
    }

    [HttpPost]
    [Authorize(Policy = Permissions.Orders.Create)]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> PaketTeslimAlindi(int id) =>
        PackageStepAsync(id, PackageFlow.HandOverBlockReason, a => a.Package!.HandedOverAt = DateTime.Now, "Paket teslim alındı");

    [HttpPost]
    [Authorize(Policy = Permissions.Orders.Create)]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> PaketYolaCikti(int id, string? courierName) =>
        PackageStepAsync(id, a => PackageFlow.DispatchBlockReason(a, courierName), a =>
        {
            a.Package!.CourierName = courierName!.Trim();
            a.Package.DispatchedAt = DateTime.Now;
        }, "Paket yola çıktı");

    [HttpPost]
    [Authorize(Policy = Permissions.Orders.Create)]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> PaketTeslimEdildi(int id) =>
        PackageStepAsync(id, PackageFlow.DeliverBlockReason, a => a.Package!.DeliveredAt = DateTime.Now, "Paket teslim edildi");

    [HttpPost]
    [Authorize(Policy = Permissions.Orders.Create)]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> PaketIptal(int id) =>
        PackageStepAsync(id, PackageFlow.CancelBlockReason, a =>
        {
            a.Status = AdisyonStatus.Iptal;
            a.ClosedAt = DateTime.Now;
            _context.Orders.RemoveRange(a.Orders.Where(o => o.IsDraft));
        }, "Paket iptal edildi");

    private async Task<IActionResult> PackageStepAsync(int id, Func<Models.Adisyon, string?> blockReason, Action<Models.Adisyon> apply, string auditAction)
    {
        var adisyon = await _context.Adisyonlar.IncludeTotals().FirstOrDefaultAsync(a => a.Id == id && a.OrderType != OrderType.Masa);
        if (adisyon == null)
        {
            return NotFound();
        }

        var error = blockReason(adisyon);
        if (error != null)
        {
            TempData["Error"] = error;
            return RedirectToAction(nameof(Paketler));
        }

        adisyon.Package ??= new PackageInfo();
        apply(adisyon);
        _context.Entry(adisyon).Property(a => a.Status).IsModified = true;
        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            TempData["Error"] = "Paket aynı sırada başka bir işlemle güncellendi. Sayfayı yenileyip tekrar deneyin.";
            return RedirectToAction(nameof(Paketler));
        }

        await _audit.LogAsync(User.FindFirstValue(ClaimTypes.NameIdentifier), User.Identity?.Name, auditAction,
            entityName: "Adisyon", entityId: id.ToString(), details: AdisyonLabel.For(adisyon));
        TempData["Info"] = auditAction + ".";
        return RedirectToAction(nameof(Paketler));
    }

    // Modül kapalıysa Süper Admin dahil kimse yeni paket açamaz; açık paketler yine yönetilir.
    private async Task<bool> CanCreatePackageAsync(OrderType type) =>
        PackageValidation.RequiredPermission(type) is string permission
        && User.HasClaim(Permissions.ClaimType, permission)
        && !await _context.FeatureSettings.AnyAsync(f => f.Key == PackageValidation.FeatureKey(type) && !f.IsEnabled);

    private async Task SetPackageButtonsAsync()
    {
        ViewBag.CanTakeaway = await CanCreatePackageAsync(OrderType.GelAl);
        ViewBag.CanDelivery = await CanCreatePackageAsync(OrderType.AdreseTeslim);
    }

    private Task<Models.Adisyon?> OpenPackageAsync(int adisyonId) =>
        _context.Adisyonlar.FirstOrDefaultAsync(a => a.Id == adisyonId && a.OrderType != OrderType.Masa && Models.Adisyon.ActiveStatuses.Contains(a.Status));

    [HttpPost]
    [Authorize(Policy = Permissions.Orders.Transfer)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MasaTasi(int adisyonId, int sourceTableId, int targetTableId)
    {
        var result = await _transfers.MoveAsync(adisyonId, targetTableId, User.FindFirstValue(ClaimTypes.NameIdentifier), User.Identity?.Name);
        TempData[result.Error == null ? "Info" : "Error"] = result.Error ?? "Masa işlemi tamamlandı.";
        return RedirectToAction(nameof(Adisyon), new { tableId = result.TableId ?? sourceTableId });
    }

    [HttpPost]
    [Authorize(Policy = Permissions.Orders.Transfer)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UrunAktar(int adisyonId, int sourceTableId, int targetTableId, [FromForm] Dictionary<int, int>? quantities)
    {
        var result = await _transfers.TransferItemsAsync(adisyonId, targetTableId, quantities ?? new(), User.FindFirstValue(ClaimTypes.NameIdentifier), User.Identity?.Name);
        TempData[result.Error == null ? "Info" : "Error"] = result.Error ?? "Ürünler aktarıldı.";
        return RedirectToAction(nameof(Adisyon), new { tableId = result.TableId ?? sourceTableId });
    }

    [HttpGet]
    public async Task<IActionResult> Yazdir(int id)
    {
        var receipt = await ReceiptBuilder.LoadAsync(_context, id);
        return receipt == null ? NotFound() : View("AdisyonPrint", receipt);
    }

    [HttpGet]
    public async Task<IActionResult> Adisyon(int tableId)
    {
        var table = await _context.RestaurantTables.FindAsync(tableId);
        if (table == null)
        {
            return NotFound();
        }

        var adisyon = await _context.Adisyonlar
            .AsNoTracking()
            .IncludeTotals()
            .Include(a => a.DiscountRequests)
            .Where(a => a.TableId == tableId && Models.Adisyon.ActiveStatuses.Contains(a.Status))
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync();

        ViewBag.Table = table;
        ViewBag.PendingVoids = adisyon == null
            ? new List<VoidRequest>()
            : await _context.VoidRequests.AsNoTracking()
                .Where(v => v.AdisyonId == adisyon.Id && v.Status == DiscountRequestStatus.Beklemede)
                .ToListAsync();
        if (adisyon != null && !adisyon.IsClosed && adisyon.OrderType == OrderType.Masa && User.HasClaim(Permissions.ClaimType, Permissions.Orders.Transfer))
        {
            ViewBag.TransferPanel = new TransferPanel(adisyon, await _transfers.LoadTargetsAsync(adisyon.TableId),
                Url.Action(nameof(MasaTasi))!, Url.Action(nameof(UrunAktar))!);
        }
        return View(adisyon);
    }

    [HttpPost]
    [Authorize(Policy = Permissions.Orders.Create)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelItem(int orderItemId, int tableId)
    {
        var error = await _voids.CancelNewItemAsync(orderItemId, User.FindFirstValue(ClaimTypes.NameIdentifier), User.Identity?.Name);
        TempData[error == null ? "Info" : "Error"] = error ?? "Kalem iptal edildi.";
        return RedirectToAction(nameof(Adisyon), new { tableId });
    }

    [HttpPost]
    [Authorize(Policy = Permissions.Orders.Create)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestItemCancel(int adisyonId, int orderItemId, int tableId, string? reason)
    {
        var error = await _voids.CreateAsync(VoidRequestService.NewRequest(User, VoidRequestType.KalemIptali, adisyonId, reason, orderItemId: orderItemId));
        TempData[error == null ? "Info" : "Error"] = error ?? "İptal talebi oluşturuldu, yönetici onayı bekleniyor.";
        return RedirectToAction(nameof(Adisyon), new { tableId });
    }

    [HttpPost]
    [Authorize(Policy = Permissions.Orders.Create)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestAdisyonCancel(int adisyonId, int tableId, string? reason)
    {
        var error = await _voids.CreateAsync(VoidRequestService.NewRequest(User, VoidRequestType.AdisyonIptali, adisyonId, reason));
        TempData[error == null ? "Info" : "Error"] = error ?? "Adisyon iptal talebi oluşturuldu, yönetici onayı bekleniyor.";
        return RedirectToAction(nameof(Adisyon), new { tableId });
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

        if (adisyon.IsClosed)
        {
            TempData["Error"] = "Bu adisyon zaten kapatılmış.";
            return BackToAdisyon(adisyon);
        }

        if (adisyon.Status != AdisyonStatus.KismiOdendi)
        {
            adisyon.Status = AdisyonStatus.Kasada;
        }
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

        return BackToAdisyon(adisyon);
    }

    private IActionResult BackToAdisyon(Models.Adisyon adisyon) =>
        adisyon.TableId is int tableId ? RedirectToAction(nameof(Adisyon), new { tableId }) : RedirectToAction(nameof(Paketler));

    [HttpPost]
    [Authorize(Policy = Permissions.Orders.Create)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestDiscount(int adisyonId, decimal amount, string? reason)
    {
        var adisyon = await _context.Adisyonlar
            .IncludeTotals()
            .FirstOrDefaultAsync(a => a.Id == adisyonId);
        if (adisyon == null)
        {
            return NotFound();
        }

        if (adisyon.IsClosed)
        {
            TempData["Error"] = "Kapatılmış bir adisyon için indirim talep edilemez.";
            return RedirectToAction(nameof(Adisyon), new { tableId = adisyon.TableId });
        }

        // Erken/dostça geri bildirim - nihai/kesin kontrol yine KasaController.ApproveDiscount'ta yapılır.
        if (amount > adisyon.RemainingTotal)
        {
            TempData["Error"] = $"İndirim tutarı hesabın kalan tutarını ({adisyon.RemainingTotal:0.00} TL) aşamaz.";
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
