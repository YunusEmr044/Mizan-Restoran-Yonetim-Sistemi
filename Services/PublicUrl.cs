using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.Extensions.Caching.Memory;
using RestoranYonetim.Data;

namespace RestoranYonetim.Services;

public enum PublicUrlSource
{
    Panel,
    Config,
    Request
}

public record LocalNetworkAddress(string Ip, string InterfaceName, bool HasGateway);

// Sitenin herkese açık adresi (QR kodları, sitemap, canonical linkler).
// Öncelik: Ayarlar ekranından girilen adres → appsettings "PublicBaseUrl" → isteğin kendi adresi.
// GÜVENLİK: Request.Host istemcinin gönderdiği, sahtelenebilir bir başlık - bu yüzden sadece son çare.
public static class PublicUrl
{
    public static bool TryNormalize(string? value, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(value) || !Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri))
        {
            return false;
        }

        if ((uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
            || !string.IsNullOrEmpty(uri.Query)
            || !string.IsNullOrEmpty(uri.Fragment)
            || !string.IsNullOrEmpty(uri.UserInfo))
        {
            return false;
        }

        normalized = uri.GetLeftPart(UriPartial.Path).TrimEnd('/');
        return true;
    }

    public static async Task<(string Url, PublicUrlSource Source)> ResolveWithSourceAsync(
        HttpContext httpContext, ApplicationDbContext context, IMemoryCache cache, IConfiguration configuration)
    {
        var content = await SiteContentCache.GetAsync(context, cache);
        if (TryNormalize(content?.PublicBaseUrl, out var panelUrl))
        {
            return (panelUrl, PublicUrlSource.Panel);
        }

        if (TryNormalize(configuration["PublicBaseUrl"], out var configUrl))
        {
            return (configUrl, PublicUrlSource.Config);
        }

        var request = httpContext.Request;
        return ($"{request.Scheme}://{request.Host}", PublicUrlSource.Request);
    }

    public static async Task<string> ResolveAsync(
        HttpContext httpContext, ApplicationDbContext context, IMemoryCache cache, IConfiguration configuration)
        => (await ResolveWithSourceAsync(httpContext, context, cache, configuration)).Url;

    // Bu bilgisayarın yerel ağdaki (özel aralık) IPv4 adresleri; varsayılan ağ geçidi olan arayüz
    // (modeme bağlı Wi-Fi/Ethernet) önce gelir, sanal adaptörler sona düşer.
    public static IReadOnlyList<LocalNetworkAddress> GetLocalNetworkAddresses()
    {
        var result = new List<LocalNetworkAddress>();
        try
        {
            foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (nic.OperationalStatus != OperationalStatus.Up
                    || nic.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
                {
                    continue;
                }

                var properties = nic.GetIPProperties();
                var hasGateway = properties.GatewayAddresses.Any(g =>
                    g.Address.AddressFamily == AddressFamily.InterNetwork && !g.Address.Equals(IPAddress.Any));

                foreach (var unicast in properties.UnicastAddresses)
                {
                    var ip = unicast.Address;
                    if (ip.AddressFamily != AddressFamily.InterNetwork || !IsPrivate(ip))
                    {
                        continue;
                    }

                    result.Add(new LocalNetworkAddress(ip.ToString(), nic.Name, hasGateway));
                }
            }
        }
        catch (NetworkInformationException)
        {
        }

        return result
            .GroupBy(a => a.Ip)
            .Select(g => g.First())
            .OrderByDescending(a => a.HasGateway)
            .ToList();
    }

    private static bool IsPrivate(IPAddress ip)
    {
        var b = ip.GetAddressBytes();
        return b[0] == 10
            || (b[0] == 172 && b[1] >= 16 && b[1] <= 31)
            || (b[0] == 192 && b[1] == 168);
    }
}
