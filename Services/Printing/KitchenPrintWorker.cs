using System.Threading.Channels;
using RestoranYonetim.Data;
using RestoranYonetim.Models;

namespace RestoranYonetim.Services.Printing;

public sealed record NetworkPrintJob(int PrinterId, string PrinterName, string Host, int Port, byte[] Data, string Label, MenuDepartment Department);

// Ağ yazıcısı işlerini arka planda gönderir ve süresi dolan tarayıcı fişlerini başarısız sayar. İş kanalı bellek içidir; yeniden deneme yoktur.
public class KitchenPrintWorker : BackgroundService
{
    public static readonly TimeSpan NetworkTimeout = TimeSpan.FromSeconds(3);

    private readonly Channel<NetworkPrintJob> _jobs = Channel.CreateBounded<NetworkPrintJob>(
        new BoundedChannelOptions(500) { FullMode = BoundedChannelFullMode.DropWrite });
    private readonly IServiceScopeFactory _scopes;
    private readonly IPrinterTransport _transport;
    private readonly PrintFailureLog _failures;
    private readonly BrowserPrintTracker _tracker;
    private readonly ILogger<KitchenPrintWorker> _logger;

    public KitchenPrintWorker(IServiceScopeFactory scopes, IPrinterTransport transport, PrintFailureLog failures, BrowserPrintTracker tracker, ILogger<KitchenPrintWorker> logger)
    {
        _scopes = scopes;
        _transport = transport;
        _failures = failures;
        _tracker = tracker;
        _logger = logger;
    }

    public async Task EnqueueAsync(NetworkPrintJob job)
    {
        if (!_jobs.Writer.TryWrite(job))
        {
            await RecordAsync(Failure(job, DateTime.Now, "Yazdırma işleri birikti."));
        }
    }

    public static async Task<PrintFailure?> SendAsync(IPrinterTransport transport, NetworkPrintJob job, DateTime now, CancellationToken ct)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(NetworkTimeout);
        try
        {
            await transport.SendAsync(job.Host, job.Port, job.Data, timeout.Token);
            return null;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            return Failure(job, now, ex is OperationCanceledException ? "Yazıcı yanıt vermedi." : ex.Message);
        }
    }

    public async Task RecordAsync(PrintFailure failure)
    {
        _failures.Add(failure);
        try
        {
            using var scope = _scopes.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            context.Notifications.Add(new Notification
            {
                Message = failure.Message,
                Link = PrintFailure.ScreenPath(failure.Department),
                RequiredPermission = PrintFailure.ScreenPermission(failure.Department)
            });
            await context.SaveChangesAsync();
            await scope.ServiceProvider.GetRequiredService<RealtimeNotifier>()
                .NotifyManyAsync(new[] { PrintFailure.RealtimeGroup(failure.Department), "notifications" }, "printer-error", failure.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Yazıcı hatası bildirimi kaydedilemedi: {Message}", failure.Message);
        }
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.WhenAll(SendLoopAsync(stoppingToken), ExpiryLoopAsync(stoppingToken));

    private async Task SendLoopAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var job in _jobs.Reader.ReadAllAsync(ct))
            {
                if (await SendAsync(_transport, job, DateTime.Now, ct) is { } failure)
                {
                    _logger.LogWarning("Ağ yazıcısına fiş gönderilemedi ({Printer} {Host}:{Port}): {Error}", job.PrinterName, job.Host, job.Port, failure.Error);
                    await RecordAsync(failure);
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
    }

    private async Task ExpiryLoopAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(2));
        try
        {
            while (await timer.WaitForNextTickAsync(ct))
            {
                var now = DateTime.Now;
                foreach (var pending in _tracker.TakeExpired(now))
                {
                    await RecordAsync(new PrintFailure(pending.Token, pending.Department, pending.PrinterName, pending.Ticket.Title, now, "Yazıcı istasyonu yanıt vermedi."));
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
    }

    private static PrintFailure Failure(NetworkPrintJob job, DateTime now, string error) =>
        new(Guid.NewGuid(), job.Department, job.PrinterName, job.Label, now, error);
}
