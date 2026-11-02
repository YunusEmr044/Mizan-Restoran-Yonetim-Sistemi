using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using RestoranYonetim.Data;
using RestoranYonetim.Services.Printing;

namespace RestoranYonetim.Hubs;

// Gerçek zamanlı ekranlar için hafif SignalR Hub'ı - hassas veri taşımaz, sadece "yenile" sinyali gönderir;
// asıl veri her zaman normal sayfa/partial isteğiyle sunucudan çekilir.
// GÜVENLİK: [Authorize] olmadan kimlik doğrulaması olmayan biri hub URL'ine doğrudan bağlanıp
// tüm bildirim akışını dinleyebilir ya da gruplara aşırı yük bindirebilirdi (DoS).
[Authorize]
public class OpsHub : Hub
{
    // GÜVENLİK: JoinGroups bu whitelist dışında bir gruba katılmaya izin vermez, aksi halde istemci
    // keyfi grup adlarıyla sunucuyu şişirebilir (DoS) ya da ileride eklenecek bir gruba önceden sızabilirdi.
    private static readonly HashSet<string> AllowedGroups = new(StringComparer.OrdinalIgnoreCase)
    {
        "notifications",
        "mutfak",
        "bar",
        "staff",
    };

    private const int MaxGroupsPerCall = 10;
    private const string PrinterGroupPrefix = "printer-";

    private readonly ApplicationDbContext _context;

    public OpsHub(ApplicationDbContext context)
    {
        _context = context;
    }

    public static int? PrinterGroupId(string group) =>
        group.StartsWith(PrinterGroupPrefix, StringComparison.OrdinalIgnoreCase)
        && int.TryParse(group.AsSpan(PrinterGroupPrefix.Length), System.Globalization.NumberStyles.None, null, out var id)
            ? id
            : null;

    public async Task JoinGroups(string[] groups)
    {
        if (groups == null || groups.Length == 0)
        {
            return;
        }

        foreach (var group in groups.Take(MaxGroupsPerCall))
        {
            var trimmed = group?.Trim();
            if (!string.IsNullOrWhiteSpace(trimmed) && AllowedGroups.Contains(trimmed))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, trimmed);
            }
            else if (trimmed != null && PrinterGroupId(trimmed) is int printerId && await CanUsePrinterAsync(printerId))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, KitchenPrintService.StationGroup(printerId));
            }
        }
    }

    // Yazıcı istasyonu grubuna yalnızca o yazıcının bölüm ekranı iznine sahip kullanıcı katılır.
    private async Task<bool> CanUsePrinterAsync(int printerId)
    {
        var department = await _context.KitchenPrinters.AsNoTracking().Where(p => p.Id == printerId).Select(p => (Models.MenuDepartment?)p.Department).FirstOrDefaultAsync();
        return department != null && Context.User?.HasClaim(Security.Permissions.ClaimType, PrintFailure.ScreenPermission(department.Value)) == true;
    }
}
