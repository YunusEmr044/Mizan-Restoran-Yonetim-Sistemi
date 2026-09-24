using Microsoft.AspNetCore.SignalR;
using RestoranYonetim.Hubs;

namespace RestoranYonetim.Services;

// Gerçek zamanlı "bir şey değişti" sinyali için ince sarmalayıcı. Best-effort: bildirim gitmezse sessizce
// yok sayılır, asıl veri normal sayfa yüklemesiyle de doğru gelir.
public class RealtimeNotifier
{
    private readonly IHubContext<OpsHub> _hub;

    public RealtimeNotifier(IHubContext<OpsHub> hub)
    {
        _hub = hub;
    }

    public Task NotifyAsync(string group, string type, string? message = null)
    {
        return _hub.Clients.Group(group).SendAsync("update", new { group, type, message });
    }

    public async Task NotifyManyAsync(IEnumerable<string> groups, string type, string? message = null)
    {
        foreach (var group in groups)
        {
            await NotifyAsync(group, type, message);
        }
    }
}
