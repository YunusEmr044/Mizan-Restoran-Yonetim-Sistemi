using System.Net.Sockets;

namespace RestoranYonetim.Services.Printing;

public interface IPrinterTransport
{
    Task SendAsync(string host, int port, byte[] data, CancellationToken ct);
}

public sealed class TcpPrinterTransport : IPrinterTransport
{
    public async Task SendAsync(string host, int port, byte[] data, CancellationToken ct)
    {
        using var client = new TcpClient();
        await client.ConnectAsync(host, port, ct);
        await using var stream = client.GetStream();
        await stream.WriteAsync(data, ct);
        await stream.FlushAsync(ct);
    }
}
