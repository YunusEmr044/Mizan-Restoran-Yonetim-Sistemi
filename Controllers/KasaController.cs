using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestoranYonetim.Data;
using RestoranYonetim.Models;
using RestoranYonetim.Security;
using RestoranYonetim.Services;

namespace RestoranYonetim.Controllers;

// Kasa ekranı: vardiya açılış/kapanış, ödeme alma/onaylama, indirim taleplerinin onayı. Adisyon kapatma sadece burada yapılabilir.
[Route("kasa")]
[Authorize(Policy = Permissions.Cash.View)]
public class KasaController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly AuditService _audit;
    private readonly CustomerService _customerService;

    public KasaController(ApplicationDbContext context, AuditService audit, CustomerService customerService)
    {
        _context = context;
        _audit = audit;
        _customerService = customerService;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        ViewBag.OpenShift = await _context.CashShifts.AsNoTracking().FirstOrDefaultAsync(s => s.ClosedAt == null);

        var bekleyenler = await _context.Adisyonlar
            .AsNoTracking()
            .Include(a => a.Table)
            .Include(a => a.Orders).ThenInclude(o => o.Items)
            .Include(a => a.Payments)
            .Where(a => a.Status == AdisyonStatus.Kasada || a.Status == AdisyonStatus.KismiOdendi)
            .OrderBy(a => a.CreatedAt)
            .ToListAsync();

        ViewBag.PendingDiscounts = await _context.DiscountRequests
            .AsNoTracking()
            .Include(d => d.Adisyon).ThenInclude(a => a!.Table)
            .Where(d => d.Status == DiscountRequestStatus.Beklemede)
            .OrderBy(d => d.CreatedAt)
            .ToListAsync();

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
            .Include(a => a.Orders).ThenInclude(o => o.Items).ThenInclude(i => i.Options)
            .Include(a => a.Payments)
            .Include(a => a.DiscountRequests)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (adisyon == null)
        {
            return NotFound();
        }

        ViewBag.OpenShift = await _context.CashShifts.AnyAsync(s => s.ClosedAt == null);
        await LoadReceiptInfoAsync();
        return View(adisyon);
    }

    // Şema eski bir kurulumda güncel değilse fiş yine de varsayılan başlıkla basılsın diye try/catch ile korunuyor.
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

    [HttpPost("adisyon/{id:int}/odeme-ekle")]
    [Authorize(Policy = Permissions.Cash.Operate)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddPayment(int id, PaymentMethod method, decimal amount)
    {
        var adisyon = await _context.Adisyonlar.Include(a => a.Payments).Include(a => a.Orders).ThenInclude(o => o.Items)
            .FirstOrDefaultAsync(a => a.Id == id);
        if (adisyon == null)
        {
            return NotFound();
        }

        // Zaten kapatılmış/iptal/iade edilmiş bir adisyona ödeme eklenemez (ör. eski sekmede açık kalan form geç gönderilirse).
        if (adisyon.Status is AdisyonStatus.Odendi or AdisyonStatus.Iptal or AdisyonStatus.Iade)
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

        if (amount > 0)
        {
            var payment = new Payment
            {
                AdisyonId = id,
                CashShiftId = openShift.Id,
                Method = method,
                Amount = amount,
                ReceivedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
                ReceivedByUserName = User.Identity?.Name
            };
            _context.Payments.Add(payment);

            // adisyon.Payments henüz bu yeni ödemeyi içermiyor - toplam elle hesaplanıyor.
            var paidTotalAfter = adisyon.Payments.Sum(p => p.Amount) + amount;
            adisyon.Status = paidTotalAfter < adisyon.GrandTotal ? AdisyonStatus.KismiOdendi : AdisyonStatus.Kasada;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                // RowVersion çakışması - kaydedilmeden tamamen geri alınır; çift ödeme yazılmasını önler.
                TempData["Error"] = "Bu adisyon aynı sırada başka bir işlemle güncellendi. Sayfayı yenileyip tekrar deneyin.";
                return RedirectToAction(nameof(Detay), new { id });
            }

            await _audit.LogAsync(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                User.Identity?.Name,
                "Ödeme eklendi",
                entityName: "Adisyon",
                entityId: id.ToString(),
                details: $"{amount:0.00} TL ({method})");
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
            .Include(a => a.Orders).ThenInclude(o => o.Items)
            .Include(a => a.Payments)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (adisyon == null)
        {
            return NotFound();
        }

        // ÖNEMLİ: RemainingTotal zaten Odendi bir adisyon için de 0'dır, o kontrol tek başına çift-onayı yakalayamaz.
        // Bu kontrol olmadan çift tıklama RegisterSpendAsync'i aynı tutarla tekrar çağırıp sadakat puanını çift sayardı.
        if (adisyon.Status is AdisyonStatus.Odendi or AdisyonStatus.Iptal or AdisyonStatus.Iade)
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

        if (!string.IsNullOrWhiteSpace(customerPhone))
        {
            await _customerService.RegisterSpendAsync(customerPhone.Trim(), customerName?.Trim(), adisyon.GrandTotal);
        }

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            // RowVersion çakışması - kapatma/müşteri puanı kaydı yarım kalmadan tamamen geri alınır.
            TempData["Error"] = "Bu adisyon aynı sırada başka bir işlemle güncellendi. Sayfayı yenileyip tekrar deneyin.";
            return RedirectToAction(nameof(Detay), new { id });
        }

        await _audit.LogAsync(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            User.Identity?.Name,
            "Ödeme onaylandı / Adisyon kapatıldı",
            entityName: "Adisyon",
            entityId: id.ToString(),
            details: $"Toplam {adisyon.GrandTotal:0.00} TL");

        TempData["Info"] = "Ödeme onaylandı, masa boşaltıldı.";
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

        var maxDiscountable = request.Adisyon.ItemsTotal - request.Adisyon.DiscountAmount;
        if (request.Amount > maxDiscountable)
        {
            TempData["Error"] = $"Bu indirim ({request.Amount:0.00} TL) hesabın kalan tutarını ({Math.Max(0, maxDiscountable):0.00} TL) aşıyor, onaylanamadı. Talebi reddedip garsonla iletişime geçin.";
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
}
