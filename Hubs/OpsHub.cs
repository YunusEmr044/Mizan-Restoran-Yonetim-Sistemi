using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

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
        }
    }
}
