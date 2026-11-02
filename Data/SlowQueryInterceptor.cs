using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace RestoranYonetim.Data;

// EF Core DbCommandInterceptor: eşiği (1000ms) aşan sorgular UYARI, SQL Server deadlock (error 1205) ayrıca ERROR olarak loglanır.
public class SlowQueryInterceptor : DbCommandInterceptor
{
    private static readonly TimeSpan SlowQueryThreshold = TimeSpan.FromMilliseconds(1000);
    private readonly ILogger<SlowQueryInterceptor> _logger;

    public SlowQueryInterceptor(ILogger<SlowQueryInterceptor> logger)
    {
        _logger = logger;
    }

    public override async ValueTask<DbDataReader> ReaderExecutedAsync(
        DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
    {
        LogIfSlow(command, eventData);
        return await base.ReaderExecutedAsync(command, eventData, result, cancellationToken);
    }

    public override DbDataReader ReaderExecuted(DbCommand command, CommandExecutedEventData eventData, DbDataReader result)
    {
        LogIfSlow(command, eventData);
        return base.ReaderExecuted(command, eventData, result);
    }

    public override Task CommandFailedAsync(DbCommand command, CommandErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        LogIfDeadlock(command, eventData.Exception);
        return base.CommandFailedAsync(command, eventData, cancellationToken);
    }

    public override void CommandFailed(DbCommand command, CommandErrorEventData eventData)
    {
        LogIfDeadlock(command, eventData.Exception);
        base.CommandFailed(command, eventData);
    }

    private void LogIfSlow(DbCommand command, CommandExecutedEventData eventData)
    {
        if (eventData.Duration >= SlowQueryThreshold)
        {
            _logger.LogWarning(
                "Yavaş SQL sorgusu tespit edildi ({DurationMs}ms, eşik {ThresholdMs}ms): {CommandText}",
                (int)eventData.Duration.TotalMilliseconds, (int)SlowQueryThreshold.TotalMilliseconds, command.CommandText);
        }
    }

    private void LogIfDeadlock(DbCommand command, Exception exception)
    {
        // SqlException'a doğrudan referans yok - Number'a reflection ile bakılıyor (tip uyuşmazsa sessizce atlanır).
        var numberProp = exception.GetType().GetProperty("Number");
        if (numberProp?.GetValue(exception) is int number && number == 1205)
        {
            _logger.LogError(exception,
                "SQL Server DEADLOCK tespit edildi - komut: {CommandText}", command.CommandText);
        }
    }
}
