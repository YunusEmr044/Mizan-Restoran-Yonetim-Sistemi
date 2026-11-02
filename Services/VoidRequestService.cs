using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RestoranYonetim.Data;
using RestoranYonetim.Extensions;
using RestoranYonetim.Models;
using RestoranYonetim.Security;
using RestoranYonetim.Services.Fiscal;
using RestoranYonetim.Services.Printing;

namespace RestoranYonetim.Services;

// İptal/iade taleplerinin açılması, onaylanması ve reddedilmesi. Dönüş değeri: başarıda null, değilse kullanıcıya gösterilecek neden.
public class VoidRequestService
{
    private readonly ApplicationDbContext _context;
    private readonly InventoryService _inventory;
    private readonly AuditService _audit;
    private readonly RealtimeNotifier _realtime;
    private readonly KitchenPrintService _kitchenPrint;
    private readonly FiscalService _fiscal;

    public VoidRequestService(ApplicationDbContext context, InventoryService inventory, AuditService audit, RealtimeNotifier realtime, KitchenPrintService kitchenPrint, FiscalService fiscal)
    {
        _context = context;
        _inventory = inventory;
        _audit = audit;
        _realtime = realtime;
        _kitchenPrint = kitchenPrint;
        _fiscal = fiscal;
    }

    private static readonly string[] StaffGroups = { "mutfak", "bar", "staff" };

    public static VoidRequest NewRequest(ClaimsPrincipal user, VoidRequestType type, int adisyonId, string? reason,
        int? orderItemId = null, int? paymentId = null, decimal? amount = null, PaymentMethod? refundMethod = null) => new()
    {
        Type = type,
        AdisyonId = adisyonId,
        OrderItemId = orderItemId,
        PaymentId = paymentId,
        Amount = amount,
        RefundMethod = refundMethod,
        Reason = reason ?? string.Empty,
        RequestedByUserId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty,
        RequestedByUserName = user.Identity?.Name
    };

    private Task<VoidRequest?> LoadPendingAsync(int requestId) =>
        _context.VoidRequests.FirstOrDefaultAsync(v => v.Id == requestId && v.Status == DiscountRequestStatus.Beklemede);

    private Task<Adisyon?> LoadAdisyonAsync(int adisyonId) =>
        _context.Adisyonlar.Include(a => a.Table).IncludeTotals().FirstOrDefaultAsync(a => a.Id == adisyonId);

    public async Task<string?> CreateAsync(VoidRequest request)
    {
        request.Reason = request.Reason?.Trim() ?? string.Empty;
        if (request.Reason.Length == 0 || request.Reason.Length > 300)
        {
            return "Lütfen en fazla 300 karakterlik bir gerekçe yazın.";
        }

        var adisyon = await LoadAdisyonAsync(request.AdisyonId);
        if (adisyon == null)
        {
            return "Adisyon bulunamadı.";
        }

        var pending = await _context.VoidRequests.AnyAsync(v =>
            v.Status == DiscountRequestStatus.Beklemede && v.AdisyonId == request.AdisyonId && v.Type == request.Type &&
            v.OrderItemId == request.OrderItemId && v.PaymentId == request.PaymentId);
        if (pending)
        {
            return "Bu işlem için zaten onay bekleyen bir talep var.";
        }

        var error = Validate(adisyon, request);
        if (error != null)
        {
            return error;
        }

        request.Status = DiscountRequestStatus.Beklemede;
        request.CreatedAt = DateTime.Now;
        _context.VoidRequests.Add(request);
        _context.Notifications.Add(new Notification
        {
            Message = $"{request.Type.Label()} talebi: {AdisyonLabel.For(adisyon)} ({request.Reason})",
            Link = "/kasa",
            RequiredPermission = Permissions.Cash.ApproveRefund
        });
        await _context.SaveChangesAsync();

        await _audit.LogAsync(request.RequestedByUserId, request.RequestedByUserName, $"{request.Type.Label()} talebi açıldı",
            entityName: "VoidRequest", entityId: request.Id.ToString(), details: request.Reason);
        await _realtime.NotifyAsync("notifications", "void-request", $"{request.Type.Label()} talebi");
        return null;
    }

    public async Task<string?> ApproveAsync(int requestId, string? approverUserId, string? approverName)
    {
        var request = await LoadPendingAsync(requestId);
        if (request == null)
        {
            return "Bu talep zaten karara bağlanmış.";
        }

        var adisyon = await LoadAdisyonAsync(request.AdisyonId);
        if (adisyon == null)
        {
            return "Adisyon bulunamadı.";
        }

        var error = Validate(adisyon, request);
        if (error != null)
        {
            return error;
        }

        CashShift? shift = null;
        if (request.Type is VoidRequestType.OdemeIptali or VoidRequestType.Iade)
        {
            shift = await _context.CashShifts.FirstOrDefaultAsync(s => s.ClosedAt == null);
            if (shift == null)
            {
                return "Önce kasa vardiyasını açın.";
            }
        }

        try
        {
            await _context.ExecuteInTransactionAsync(async () =>
            {
                switch (request.Type)
                {
                    case VoidRequestType.KalemIptali:
                        CancelItem(adisyon, request.OrderItemId!.Value);
                        break;
                    case VoidRequestType.OdemeIptali:
                        var original = adisyon.Payments.Single(p => p.Id == request.PaymentId);
                        AddReversal(adisyon, shift!, -original.Amount, original.Method, original.Id, $"Ödeme iptali: {request.Reason}", approverUserId, approverName, VoidRules.StatusAfterPaymentVoid);
                        break;
                    case VoidRequestType.Iade:
                        AddReversal(adisyon, shift!, -request.Amount!.Value, request.RefundMethod ?? PaymentMethod.Nakit, null, $"İade: {request.Reason}", approverUserId, approverName, VoidRules.StatusAfterRefund);
                        break;
                    case VoidRequestType.AdisyonIptali:
                        await CancelAdisyonAsync(adisyon);
                        break;
                }

                request.Status = DiscountRequestStatus.Onaylandi;
                request.DecidedAt = DateTime.Now;
                request.DecidedByUserName = approverName;
            });
        }
        catch (DbUpdateConcurrencyException)
        {
            return "Bu adisyon aynı sırada başka bir işlemle güncellendi. Sayfayı yenileyip tekrar deneyin.";
        }

        await _audit.LogAsync(approverUserId, approverName, $"{request.Type.Label()} onaylandı",
            entityName: "VoidRequest", entityId: request.Id.ToString(),
            details: $"Adisyon #{adisyon.Id}, talep eden: {request.RequestedByUserName}, gerekçe: {request.Reason}");
        await _realtime.NotifyManyAsync(StaffGroups, "refresh");
        if (request.Type == VoidRequestType.KalemIptali)
        {
            await _kitchenPrint.DispatchVoidAsync(request.OrderItemId!.Value, request.Reason, approverName);
        }
        else
        {
            // Kesilmiş mali fişi olan adisyon: tamamen iade/iptalde fiş iptali denenir, aksi halde iade belgesi uyarısı düşer.
            await _fiscal.OnRefundOrCancelAsync(adisyon.Id, adisyon.Status is AdisyonStatus.Iade or AdisyonStatus.Iptal, approverName);
        }
        return null;
    }

    public async Task<string?> RejectAsync(int requestId, string? approverUserId, string? approverName)
    {
        var request = await LoadPendingAsync(requestId);
        if (request == null)
        {
            return "Bu talep zaten karara bağlanmış.";
        }

        request.Status = DiscountRequestStatus.Reddedildi;
        request.DecidedAt = DateTime.Now;
        request.DecidedByUserName = approverName;
        await _context.SaveChangesAsync();

        await _audit.LogAsync(approverUserId, approverName, $"{request.Type.Label()} reddedildi",
            entityName: "VoidRequest", entityId: request.Id.ToString(), details: request.Reason);
        return null;
    }

    public async Task<string?> CancelNewItemAsync(int orderItemId, string? userId, string? userName)
    {
        var item = await _context.OrderItems.AsNoTracking().FirstOrDefaultAsync(i => i.Id == orderItemId);
        if (item == null)
        {
            return "Kalem bulunamadı.";
        }
        if (!VoidRules.CanCancelItemDirectly(item))
        {
            return "Bu kalem hazırlanmaya başlanmış - iptal için onay talebi açın.";
        }

        var adisyonId = await _context.Orders.Where(o => o.Id == item.OrderId).Select(o => o.AdisyonId).FirstOrDefaultAsync();
        var adisyon = adisyonId == null ? null : await LoadAdisyonAsync(adisyonId.Value);
        if (adisyon == null)
        {
            return "Adisyon bulunamadı.";
        }

        var tracked = adisyon.Orders.SelectMany(o => o.Items).Single(i => i.Id == orderItemId);
        var error = VoidRules.CancelItemBlockReason(adisyon, tracked);
        if (error != null)
        {
            return error;
        }

        try
        {
            await _context.ExecuteInTransactionAsync(async () =>
            {
                await _inventory.RestoreForItemsAsync(tracked.OrderId, new[] { tracked });
                CancelItem(adisyon, orderItemId);
            });
        }
        catch (DbUpdateConcurrencyException)
        {
            return "Bu adisyon aynı sırada başka bir işlemle güncellendi. Sayfayı yenileyip tekrar deneyin.";
        }

        await _audit.LogAsync(userId, userName, "Kalem iptal edildi", entityName: "OrderItem", entityId: orderItemId.ToString(),
            details: $"{tracked.MenuItemName} x{tracked.Quantity}, adisyon #{adisyon.Id}");
        await _realtime.NotifyManyAsync(StaffGroups, "refresh");
        await _kitchenPrint.DispatchVoidAsync(orderItemId, null, userName);
        return null;
    }

    private static string? Validate(Adisyon adisyon, VoidRequest request)
    {
        switch (request.Type)
        {
            case VoidRequestType.KalemIptali:
                var item = adisyon.Orders.SelectMany(o => o.Items).FirstOrDefault(i => i.Id == request.OrderItemId);
                return item == null ? "Kalem bu hesaba ait değil." : VoidRules.CancelItemBlockReason(adisyon, item);
            case VoidRequestType.OdemeIptali:
                var payment = adisyon.Payments.FirstOrDefault(p => p.Id == request.PaymentId);
                return payment == null ? "Ödeme bu hesaba ait değil." : VoidRules.PaymentVoidBlockReason(adisyon, payment);
            case VoidRequestType.Iade:
                return VoidRules.RefundBlockReason(adisyon, request.Amount ?? 0);
            case VoidRequestType.AdisyonIptali:
                return VoidRules.AdisyonCancelBlockReason(adisyon);
            default:
                return "Geçersiz talep türü.";
        }
    }

    // Adisyon satırı da güncellenir ki RowVersion kontrolü eşzamanlı ödemeyle çakışmayı yakalasın.
    private void CancelItem(Adisyon adisyon, int orderItemId)
    {
        _context.Entry(adisyon).Property(a => a.Status).IsModified = true;
        var order = adisyon.Orders.Single(o => o.Items.Any(i => i.Id == orderItemId));
        order.Items.Single(i => i.Id == orderItemId).Status = OrderStatus.IptalEdildi;
        if (order.Items.All(i => i.Status == OrderStatus.IptalEdildi))
        {
            order.Status = OrderStatus.IptalEdildi;
        }
        else
        {
            OrderStatusHelper.Recompute(order);
        }
    }

    // Yeni durum, ters kayıt dahil edilerek hesaplanır; kayıt adisyonun listesinde bırakılmaz ki transaction tekrar denenirse iki kez eklenmesin.
    private void AddReversal(Adisyon adisyon, CashShift shift, decimal amount, PaymentMethod method, int? reversesPaymentId, string note,
        string? userId, string? userName, Func<Adisyon, AdisyonStatus> statusAfter)
    {
        var reversal = new Payment
        {
            AdisyonId = adisyon.Id,
            CashShiftId = shift.Id,
            Method = method,
            Amount = amount,
            ReversesPaymentId = reversesPaymentId,
            Note = note.Length > 300 ? note[..300] : note,
            ReceivedByUserId = userId,
            ReceivedByUserName = userName
        };
        adisyon.Payments.Add(reversal);
        adisyon.Status = statusAfter(adisyon);
        adisyon.Payments.Remove(reversal);
        _context.Payments.Add(reversal);
    }

    private async Task CancelAdisyonAsync(Adisyon adisyon)
    {
        var unprepared = adisyon.Orders
            .Where(o => !o.IsDraft)
            .SelectMany(o => o.Items)
            .Where(i => i.Status == OrderStatus.Yeni)
            .ToList();
        foreach (var group in unprepared.GroupBy(i => i.OrderId))
        {
            await _inventory.RestoreForItemsAsync(group.Key, group.ToList());
        }

        foreach (var order in adisyon.Orders)
        {
            foreach (var item in order.Items)
            {
                item.Status = OrderStatus.IptalEdildi;
            }
            order.Status = OrderStatus.IptalEdildi;
        }

        adisyon.Status = AdisyonStatus.Iptal;
        adisyon.ClosedAt = DateTime.Now;
        if (adisyon.Table != null)
        {
            adisyon.Table.Status = TableStatus.Bos;
        }
    }
}
