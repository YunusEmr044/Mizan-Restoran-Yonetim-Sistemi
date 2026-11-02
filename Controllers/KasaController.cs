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

// Kasa ekranı: vardiya açılış/kapanış, ödeme alma/onaylama, indirim taleplerinin onayı. Adisyon kapatma sadece burada yapılabilir.
[Route("kasa")]
[Authorize(Policy = Permissions.Cash.View)]
public class KasaController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly AuditService _audit;
    private readonly CustomerService _customerService;
    private readonly VoidRequestService _voids;
    private readonly TableTransferService _transfers;
    private readonly FiscalService _fiscal;

    public KasaController(ApplicationDbContext context, AuditService audit, CustomerService customerService, VoidRequestService voids, TableTransferService transfers, FiscalService fiscal)
    {
        _voids = voids;
        _transfers = transfers;
        _context = context;
        _audit = audit;
        _fiscal = fiscal;
        _customerService = customerService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        ViewBag.OpenShift = await _context.CashShifts.AsNoTracking().FirstOrDefaultAsync(s => s.ClosedAt == null);

        var bekleyenler = await _context.Adisyonlar
            .AsNoTracking()
            .Include(a => a.Table)
            .IncludeTotals()
            .Where(a => a.Status == AdisyonStatus.Kasada || a.Status == AdisyonStatus.KismiOdendi)
            .OrderBy(a => a.CreatedAt)
            .AsSplitQuery()
            .ToListAsync();

        ViewBag.PendingDiscounts = await _context.DiscountRequests
            .AsNoTracking()
            .Include(d => d.Adisyon).ThenInclude(a => a!.Table)
            .Where(d => d.Status == DiscountRequestStatus.Beklemede)
            .OrderBy(d => d.CreatedAt)
            .ToListAsync();

        ViewBag.PendingVoids = await _context.VoidRequests
            .AsNoTracking()
            .Include(v => v.Adisyon).ThenInclude(a => a!.Table)
            .Include(v => v.OrderItem)
            .Include(v => v.Payment)
            .Where(v => v.Status == DiscountRequestStatus.Beklemede)
            .OrderBy(v => v.CreatedAt)
            .ToListAsync();

        var today = DateTime.Today;
        ViewBag.ClosedToday = await _context.Adisyonlar
            .AsNoTracking()
            .Include(a => a.Table)
            .Include(a => a.Payments)
            .Where(a => (a.Status == AdisyonStatus.Odendi || a.Status == AdisyonStatus.Iade) && a.ClosedAt >= today)
            .OrderByDescending(a => a.ClosedAt)
            .Take(20)
            .ToListAsync();

        ViewBag.PendingReceipts = await _fiscal.PendingAsync();
        return View(bekleyenler);
    }

    [HttpPost("vardiya/ac")]
    [Authorize(Policy = Permissions.Cash.Operate)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> OpenShift(decimal openingAmount)
    {
        // Negatif başlangıç tutarı ExpectedCash/CashDifference hesaplarını anlamsızlaştırır.
        if (openingAmount < 0)
        {
            TempData["Error"] = "Başlangıç tutarı negatif olamaz.";
            return RedirectToAction(nameof(Index));
        }

        var existing = await _context.CashShifts.AnyAsync(s => s.ClosedAt == null);
        if (existing)
        {
            TempData["Error"] = "Zaten açık bir vardiya var.";
            return RedirectToAction(nameof(Index));
        }

        _context.CashShifts.Add(new CashShift
        {
            OpeningAmount = openingAmount,
            OpenedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
            OpenedByUserName = User.Identity?.Name
        });

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // ÖNEMLİ: AnyAsync ön-kontrolü ile INSERT arasında TOCTOU yarışı olabilir; DB'deki unique filtreli indeks (ClosedAt IS NULL) ikinci INSERT'i reddeder, burada 500 yerine kullanıcı dostu mesaja çevriliyor.
            TempData["Error"] = "Zaten açık bir vardiya var (başka bir kullanıcı aynı anda açmış olabilir). Sayfayı yenileyin.";
            return RedirectToAction(nameof(Index));
        }

        await _audit.LogAsync(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            User.Identity?.Name,
            "Kasa vardiyası açıldı",
            entityName: "CashShift",
            details: $"Başlangıç tutarı: {openingAmount:0.00} TL");

        TempData["Info"] = "Kasa vardiyası açıldı.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("vardiya/kapat")]
    [Authorize(Policy = Permissions.Cash.Operate)]
    public async Task<IActionResult> CloseShiftForm()
    {
        var shift = await _context.CashShifts.Include(s => s.Payments).FirstOrDefaultAsync(s => s.ClosedAt == null);
        if (shift == null)
        {
            TempData["Error"] = "Açık vardiya yok.";
            return RedirectToAction(nameof(Index));
        }
        return View("CloseShift", shift);
    }

    [HttpPost("vardiya/kapat")]
    [Authorize(Policy = Permissions.Cash.Operate)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CloseShift(int shiftId, decimal countedAmount)
    {
        if (countedAmount < 0)
        {
            TempData["Error"] = "Sayılan tutar negatif olamaz.";
            return RedirectToAction(nameof(CloseShiftForm));
        }

        var shift = await _context.CashShifts.Include(s => s.Payments).FirstOrDefaultAsync(s => s.Id == shiftId && s.ClosedAt == null);
        if (shift == null)
        {
            return NotFound();
        }

        shift.ClosingCountedAmount = countedAmount;
        shift.ClosedAt = DateTime.Now;
        shift.ClosedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        shift.ClosedByUserName = User.Identity?.Name;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            // RowVersion çakışması - "son yazan kazanır" yerine kullanıcıya açıkça bildirilir.
            TempData["Error"] = "Bu vardiya aynı sırada başka bir işlemle güncellendi (belki de zaten kapatılmış). Sayfayı yenileyin.";
            return RedirectToAction(nameof(Index));
        }

        await _audit.LogAsync(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            User.Identity?.Name,
            "Kasa vardiyası kapatıldı",
            entityName: "CashShift",
            entityId: shift.Id.ToString(),
            details: $"Beklenen: {shift.ExpectedCash:0.00} TL, Sayılan: {countedAmount:0.00} TL, Fark: {shift.CashDifference:0.00} TL");

        TempData["Info"] = $"Vardiya kapatıldı. Kasa farkı: {shift.CashDifference:0.00} TL";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("adisyon/{id:int}")]
    public async Task<IActionResult> Detay(int id)
    {
        var adisyon = await _context.Adisyonlar
            .AsNoTracking()
            .Include(a => a.Table)
            .IncludeTotals()
            .Include(a => a.Payments).ThenInclude(p => p.Items)
            .Include(a => a.DiscountRequests)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (adisyon == null)
        {
            return NotFound();
        }

        ViewBag.OpenShift = await _context.CashShifts.AnyAsync(s => s.ClosedAt == null);
        ViewBag.PendingVoids = await _context.VoidRequests.AsNoTracking()
            .Where(v => v.AdisyonId == id && v.Status == DiscountRequestStatus.Beklemede)
            .ToListAsync();
        if (!adisyon.IsClosed && adisyon.OrderType == OrderType.Masa && User.HasClaim(Permissions.ClaimType, Permissions.Orders.Transfer))
        {
            ViewBag.TransferPanel = new TransferPanel(adisyon, await _transfers.LoadTargetsAsync(adisyon.TableId),
                Url.Action(nameof(MasaTasi), new { id })!, Url.Action(nameof(UrunAktar), new { id })!);
        }
        ViewBag.FiscalReceipt = await _context.FiscalReceipts.AsNoTracking().FirstOrDefaultAsync(r => r.AdisyonId == id);
        ViewBag.CardOnDevice = !adisyon.IsClosed && (await _fiscal.CardOptionAsync(await _fiscal.BranchOfAsync(id))).SupportsCard;
        ViewBag.SplitLines = ItemSplit.Lines(SplitItems(adisyon), adisyon.Payments);
        return View(adisyon);
    }

    [HttpPost("odeme/{paymentId:int}/iptal-talebi")]
    [Authorize(Policy = Permissions.Cash.Operate)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestPaymentVoid(int paymentId, string? reason)
    {
        var adisyonId = await _context.Payments.Where(p => p.Id == paymentId).Select(p => (int?)p.AdisyonId).FirstOrDefaultAsync();
        if (adisyonId == null)
        {
            return NotFound();
        }

        var error = await _voids.CreateAsync(VoidRequestService.NewRequest(User, VoidRequestType.OdemeIptali, adisyonId.Value, reason, paymentId: paymentId));
        TempData[error == null ? "Info" : "Error"] = error ?? "Ödeme iptal talebi oluşturuldu, yönetici onayı bekleniyor.";
        return RedirectToAction(nameof(Detay), new { id = adisyonId });
    }

    [HttpPost("adisyon/{id:int}/iade-talebi")]
    [Authorize(Policy = Permissions.Cash.Operate)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestRefund(int id, decimal amount, PaymentMethod method, string? reason)
    {
        var error = await _voids.CreateAsync(VoidRequestService.NewRequest(User, VoidRequestType.Iade, id, reason, amount: amount, refundMethod: method));
        TempData[error == null ? "Info" : "Error"] = error ?? "İade talebi oluşturuldu, yönetici onayı bekleniyor.";
        return RedirectToAction(nameof(Detay), new { id });
    }

    [HttpPost("talep/{id:int}/onayla")]
    [Authorize(Policy = Permissions.Cash.ApproveRefund)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveVoid(int id)
    {
        var error = await _voids.ApproveAsync(id, User.FindFirstValue(ClaimTypes.NameIdentifier), User.Identity?.Name);
        TempData[error == null ? "Info" : "Error"] = error ?? "Talep onaylandı.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("talep/{id:int}/reddet")]
    [Authorize(Policy = Permissions.Cash.ApproveRefund)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectVoid(int id)
    {
        var error = await _voids.RejectAsync(id, User.FindFirstValue(ClaimTypes.NameIdentifier), User.Identity?.Name);
        TempData[error == null ? "Info" : "Error"] = error ?? "Talep reddedildi.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("adisyon/{id:int}/masa-tasi")]
    [Authorize(Policy = Permissions.Orders.Transfer)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MasaTasi(int id, int targetTableId)
    {
        var result = await _transfers.MoveAsync(id, targetTableId, User.FindFirstValue(ClaimTypes.NameIdentifier), User.Identity?.Name);
        TempData[result.Error == null ? "Info" : "Error"] = result.Error ?? "Masa işlemi tamamlandı.";
        return RedirectToAction(nameof(Detay), new { id = result.AdisyonId ?? id });
    }

    [HttpPost("adisyon/{id:int}/urun-aktar")]
    [Authorize(Policy = Permissions.Orders.Transfer)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UrunAktar(int id, int targetTableId, [FromForm] Dictionary<int, int>? quantities)
    {
        var result = await _transfers.TransferItemsAsync(id, targetTableId, quantities ?? new(), User.FindFirstValue(ClaimTypes.NameIdentifier), User.Identity?.Name);
        TempData[result.Error == null ? "Info" : "Error"] = result.Error ?? "Ürünler aktarıldı.";
        return RedirectToAction(nameof(Detay), new { id = result.AdisyonId ?? id });
    }

    [HttpGet("adisyon/{id:int}/yazdir")]
    public async Task<IActionResult> Yazdir(int id)
    {
        var receipt = await ReceiptBuilder.LoadAsync(_context, id);
        return receipt == null ? NotFound() : View("AdisyonPrint", receipt);
    }

    [HttpPost("adisyon/{id:int}/odeme-ekle")]
    [Authorize(Policy = Permissions.Cash.Operate)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddPayment(int id, PaymentMethod method, decimal amount, bool posCharge = false, [FromForm] Dictionary<int, int>? items = null)
    {
        var adisyon = await _context.Adisyonlar.IncludeTotals()
            .Include(a => a.Payments).ThenInclude(p => p.Items)
            .FirstOrDefaultAsync(a => a.Id == id);
        if (adisyon == null)
        {
            return NotFound();
        }

        // Zaten kapatılmış/iptal/iade edilmiş bir adisyona ödeme eklenemez (ör. eski sekmede açık kalan form geç gönderilirse).
        if (adisyon.IsClosed)
        {
            TempData["Error"] = "Bu adisyon zaten kapatılmış (ödendi/iptal/iade) - yeni ödeme eklenemez.";
            return RedirectToAction(nameof(Detay), new { id });
        }

        var openShift = await _context.CashShifts.AsNoTracking().FirstOrDefaultAsync(s => s.ClosedAt == null);
        if (openShift == null)
        {
            TempData["Error"] = "Ödeme alabilmek için önce kasa vardiyasını açmalısınız.";
            return RedirectToAction(nameof(Detay), new { id });
        }

        // Ürün bazlı ödemede tutarı sunucu hesaplar; formdan gelen tutar kullanılmaz.
        var chosen = items?.Where(i => i.Value != 0).ToDictionary(i => i.Key, i => i.Value);
        List<PaymentItem>? paidItems = null;
        if (chosen is { Count: > 0 })
        {
            var lines = ItemSplit.Lines(SplitItems(adisyon), adisyon.Payments);
            var split = ItemSplit.Amount(lines, chosen, adisyon.ItemsTotal, adisyon.GrandTotal, adisyon.RemainingTotal);
            if (split.Error != null)
            {
                TempData["Error"] = split.Error;
                return RedirectToAction(nameof(Detay), new { id });
            }
            amount = split.Amount;
            paidItems = chosen.Select(c => new PaymentItem { OrderItemId = c.Key, Quantity = c.Value }).ToList();
        }

        if (amount <= 0)
        {
            TempData["Error"] = "Lütfen sıfırdan büyük bir ödeme tutarı girin.";
            return RedirectToAction(nameof(Detay), new { id });
        }

        var remaining = adisyon.RemainingTotal;
        if (remaining <= 0)
        {
            TempData["Error"] = "Bu adisyonun ödenecek tutarı kalmadı - ödemeyi onaylayarak kapatabilirsiniz.";
            return RedirectToAction(nameof(Detay), new { id });
        }

        decimal change = 0;
        if (amount > remaining)
        {
            if (method != PaymentMethod.Nakit)
            {
                TempData["Error"] = $"Ödeme tutarı kalan tutarı ({remaining:0.00} TL) aşamaz.";
                return RedirectToAction(nameof(Detay), new { id });
            }

            change = amount - remaining;
            amount = remaining;
        }

        // Kart yazar kasa POS'undan çekilir; onay gelmezse ödeme kaydedilmez.
        string? posNote = null;
        if (posCharge && method is PaymentMethod.KrediKarti or PaymentMethod.BankaKarti)
        {
            var card = await _fiscal.ChargeCardAsync(await _fiscal.BranchOfAsync(id), amount, "ADS-" + id);
            await _audit.LogAsync(User.FindFirstValue(ClaimTypes.NameIdentifier), User.Identity?.Name,
                card?.Approved == true ? "POS kart tahsilatı onaylandı" : "POS kart tahsilatı onaylanmadı",
                entityName: "Adisyon", entityId: id.ToString(),
                details: $"{amount:0.00} TL{(card?.AuthCode == null ? "" : $", onay {card.AuthCode}")}{(card?.Message == null ? "" : $" - {card.Message}")}");
            if (card is not { Approved: true })
            {
                TempData["Error"] = card == null
                    ? "Şubenin yazar kasa cihazı kart tahsilatını desteklemiyor ya da tanımlı değil; \"Kartı POS'tan çek\" kapalıyken ödemeyi ekleyin."
                    : $"Kart işlemi onaylanmadı: {card.Message}";
                return RedirectToAction(nameof(Detay), new { id });
            }
            posNote = $"POS onay {card.AuthCode}{(card.Masked == null ? "" : $" · ****{card.Masked}")}";
        }

        var itemsNote = paidItems == null ? null : "Ürünler: " + string.Join(", ", paidItems.Select(p =>
            $"{p.Quantity}× {SplitItems(adisyon).First(i => i.Id == p.OrderItemId).MenuItemName}"));
        var note = string.Join(" · ", new[] { posNote, itemsNote }.Where(n => n != null));
        _context.Payments.Add(new Payment
        {
            AdisyonId = id,
            CashShiftId = openShift.Id,
            Method = method,
            Amount = amount,
            Note = note.Length == 0 ? null : note[..Math.Min(note.Length, 300)],
            Items = paidItems ?? new(),
            ReceivedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
            ReceivedByUserName = User.Identity?.Name
        });

        adisyon.Status = amount < remaining ? AdisyonStatus.KismiOdendi : AdisyonStatus.Kasada;
        // Durum aynı kalsa da adisyon satırı güncellenir; böylece RowVersion kontrolü eşzamanlı ikinci ödemeyi reddeder.
        _context.Entry(adisyon).Property(a => a.Status).IsModified = true;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            // RowVersion çakışması - kaydedilmeden tamamen geri alınır; çift ödeme yazılmasını önler.
            TempData["Error"] = posNote == null
                ? "Bu adisyon aynı sırada başka bir işlemle güncellendi. Sayfayı yenileyip tekrar deneyin."
                : $"Kart POS'ta onaylandı ({posNote}) ama adisyon aynı sırada değiştiği için ödeme kaydedilemedi. Kartı tekrar çekmeyin: \"Kartı POS'tan çek\" kapalıyken {amount:0.00} TL ödemeyi elle ekleyin.";
            return RedirectToAction(nameof(Detay), new { id });
        }

        await _audit.LogAsync(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            User.Identity?.Name,
            "Ödeme eklendi",
            entityName: "Adisyon",
            entityId: id.ToString(),
            details: change > 0 ? $"{amount:0.00} TL ({method}), para üstü: {change:0.00} TL" : $"{amount:0.00} TL ({method})");

        if (change > 0)
        {
            TempData["Info"] = $"{amount:0.00} TL tahsil edildi. Müşteriye para üstü: {change:0.00} TL";
        }

        return RedirectToAction(nameof(Detay), new { id });
    }

    // Ödeme tamamen alınmadan adisyon kapanmaz. Müşteri telefonu isteğe bağlı - sadakat puanı için.
    [HttpPost("adisyon/{id:int}/onayla")]
    [Authorize(Policy = Permissions.Cash.ApprovePayment)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmPayment(int id, string? customerPhone, string? customerName)
    {
        var adisyon = await _context.Adisyonlar
            .Include(a => a.Table)
            .IncludeTotals()
            .FirstOrDefaultAsync(a => a.Id == id);

        if (adisyon == null)
        {
            return NotFound();
        }

        // ÖNEMLİ: RemainingTotal zaten Odendi bir adisyon için de 0'dır, o kontrol tek başına çift-onayı yakalayamaz.
        // Bu kontrol olmadan çift tıklama RegisterSpendAsync'i aynı tutarla tekrar çağırıp sadakat puanını çift sayardı.
        if (adisyon.IsClosed)
        {
            TempData["Error"] = "Bu adisyon zaten ödeme onaylanarak kapatılmış - tekrar onaylanamaz.";
            return RedirectToAction(nameof(Detay), new { id });
        }

        if (adisyon.RemainingTotal > 0)
        {
            TempData["Error"] = $"Ödeme eksik. Kalan tutar: {adisyon.RemainingTotal:0.00} TL";
            return RedirectToAction(nameof(Detay), new { id });
        }

        adisyon.Status = AdisyonStatus.Odendi;
        adisyon.ClosedAt = DateTime.Now;
        if (adisyon.Table != null)
        {
            adisyon.Table.Status = TableStatus.Bos;
        }
        var receipt = await _fiscal.CreateForClosingAsync(adisyon);

        try
        {
            await _context.ExecuteInTransactionAsync(async () =>
            {
                if (!string.IsNullOrWhiteSpace(customerPhone))
                {
                    await _customerService.RegisterSpendAsync(customerPhone.Trim(), customerName?.Trim(), adisyon.GrandTotal);
                }
            });
        }
        catch (DbUpdateConcurrencyException)
        {
            // RowVersion çakışması - kapatma/müşteri puanı kaydı yarım kalmadan tamamen geri alınır.
            TempData["Error"] = "Bu adisyon aynı sırada başka bir işlemle güncellendi. Sayfayı yenileyip tekrar deneyin.";
            return RedirectToAction(nameof(Detay), new { id });
        }
        catch (DbUpdateException) when (receipt != null)
        {
            // FiscalReceipt.AdisyonId benzersiz: aynı adisyon için ikinci mali fiş açılamaz.
            TempData["Error"] = "Bu adisyon için mali fiş zaten açılmış.";
            return RedirectToAction(nameof(Detay), new { id });
        }

        await _audit.LogAsync(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            User.Identity?.Name,
            "Ödeme onaylandı / Adisyon kapatıldı",
            entityName: "Adisyon",
            entityId: id.ToString(),
            details: $"Toplam {adisyon.GrandTotal:0.00} TL");

        TempData["Info"] = "Ödeme onaylandı, masa boşaltıldı." + await FiscalMessageAsync(receipt);
        return RedirectToAction(nameof(Index));
    }

    private async Task<string> FiscalMessageAsync(FiscalReceipt? receipt)
    {
        if (receipt == null)
        {
            return string.Empty;
        }
        if (receipt.Status == FiscalReceiptStatus.ElleGirilecek)
        {
            return " Mali fişi cihazdan kesip numarasını \"Mali fiş bekleyenler\" bölümüne girin.";
        }
        var outcome = await _fiscal.TryPrintAsync(receipt.Id, User.Identity?.Name);
        return outcome.Status switch
        {
            FiscalReceiptStatus.Kesildi => $" Mali fiş kesildi: #{outcome.ReceiptNo}.",
            FiscalReceiptStatus.Belirsiz => $" Mali fiş sonucu belirsiz ({outcome.Message}). Cihazda fiş çıktı mı kontrol edin.",
            _ => $" Mali fiş kesilemedi ({outcome.Message}); \"Mali fiş bekleyenler\" bölümünden tekrar deneyin."
        };
    }

    [HttpPost("fis/{receiptId:int}/tekrar")]
    [Authorize(Policy = Permissions.Cash.Operate)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RetryReceipt(int receiptId, bool confirmedNotPrinted)
    {
        var error = await _fiscal.RetryAsync(receiptId, confirmedNotPrinted, User.Identity?.Name);
        TempData[error == null ? "Info" : "Error"] = error ?? "Mali fiş kesildi.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("fis/{receiptId:int}/no")]
    [Authorize(Policy = Permissions.Cash.Operate)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EnterReceiptNumber(int receiptId, string? receiptNo, string? zNo)
    {
        var error = await _fiscal.EnterNumberAsync(receiptId, receiptNo, zNo, User.Identity?.Name);
        TempData[error == null ? "Info" : "Error"] = error ?? "Mali fiş numarası kaydedildi.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("fis/{receiptId:int}/fissiz")]
    [Authorize(Policy = Permissions.Cash.ApproveRefund)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CloseWithoutReceipt(int receiptId, string? reason)
    {
        var error = await _fiscal.CloseWithoutReceiptAsync(receiptId, reason, User.Identity?.Name);
        TempData[error == null ? "Info" : "Error"] = error ?? "Mali fiş kaydı fişsiz kapatıldı.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("indirim/{id:int}/onayla")]
    [Authorize(Policy = Permissions.Cash.ApprovePayment)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApproveDiscount(int id)
    {
        // ÖNEMLİ: DiscountAmount hesap tutarını aşacak biçimde kaydedilebiliyordu - onay anında sipariş toplamıyla karşılaştırılıp aşan talepler reddediliyor.
        var request = await _context.DiscountRequests
            .Include(d => d.Adisyon!).ThenInclude(a => a.Orders).ThenInclude(o => o.Items).ThenInclude(i => i.Options)
            .Include(d => d.Adisyon!).ThenInclude(a => a.Payments)
            .FirstOrDefaultAsync(d => d.Id == id);
        if (request == null)
        {
            return NotFound();
        }

        if (request.Adisyon == null)
        {
            return NotFound();
        }

        // ÖNEMLİ: talep zaten onaylanmışken tekrar çağrılırsa (çift tıklama) indirim adisyona ikinci kez eklenebilir - sadece "Beklemede" talepler karara bağlanır.
        if (request.Status != DiscountRequestStatus.Beklemede)
        {
            TempData["Error"] = "Bu indirim talebi zaten karara bağlanmış.";
            return RedirectToAction(nameof(Index));
        }

        if (request.Adisyon.IsClosed)
        {
            TempData["Error"] = "Bu adisyon kapatılmış - indirim uygulanamaz. Talebi reddedin.";
            return RedirectToAction(nameof(Index));
        }

        var maxDiscountable = request.Adisyon.RemainingTotal;
        if (request.Amount > maxDiscountable)
        {
            TempData["Error"] = $"Bu indirim ({request.Amount:0.00} TL) hesabın kalan tutarını ({maxDiscountable:0.00} TL) aşıyor, onaylanamadı. Talebi reddedip garsonla iletişime geçin.";
            return RedirectToAction(nameof(Index));
        }

        request.Status = DiscountRequestStatus.Onaylandi;
        request.DecidedAt = DateTime.Now;
        request.DecidedByUserName = User.Identity?.Name;
        request.Adisyon.DiscountAmount += request.Amount;
        request.Adisyon.DiscountReason = request.Reason;

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            TempData["Error"] = "Bu adisyon aynı sırada başka bir işlemle güncellendi. Sayfayı yenileyip tekrar deneyin.";
            return RedirectToAction(nameof(Index));
        }

        await _audit.LogAsync(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            User.Identity?.Name,
            "İndirim talebi onaylandı",
            entityName: "DiscountRequest",
            entityId: id.ToString(),
            details: $"{request.Amount:0.00} TL, talep eden: {request.RequestedByUserName}, gerekçe: {request.Reason}");

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("indirim/{id:int}/reddet")]
    [Authorize(Policy = Permissions.Cash.ApprovePayment)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RejectDiscount(int id)
    {
        var request = await _context.DiscountRequests.FindAsync(id);
        if (request == null)
        {
            return NotFound();
        }

        // Zaten onaylanmış bir talep reddedilirse, adisyona eklenmiş DiscountAmount geri alınmadığı için tutarsızlık oluşur.
        if (request.Status != DiscountRequestStatus.Beklemede)
        {
            TempData["Error"] = "Bu indirim talebi zaten karara bağlanmış.";
            return RedirectToAction(nameof(Index));
        }

        request.Status = DiscountRequestStatus.Reddedildi;
        request.DecidedAt = DateTime.Now;
        request.DecidedByUserName = User.Identity?.Name;
        await _context.SaveChangesAsync();

        await _audit.LogAsync(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            User.Identity?.Name,
            "İndirim talebi reddedildi",
            entityName: "DiscountRequest",
            entityId: id.ToString(),
            details: $"{request.Amount:0.00} TL, talep eden: {request.RequestedByUserName}, gerekçe: {request.Reason}");

        return RedirectToAction(nameof(Index));
    }

    private static IEnumerable<OrderItem> SplitItems(Adisyon adisyon) =>
        adisyon.Orders.Where(o => !o.IsDraft && o.Status != OrderStatus.IptalEdildi).SelectMany(o => o.Items);
}
