using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using RestoranYonetim.Data;

namespace RestoranYonetim.Services;

// Otomatik veritabanı yedekleme (SQL Server Agent SQL Express'te yok). Yedek dosyasını asıl
// yazan SQL Server SERVİS HESABIDIR, bağlanan Windows kullanıcısı değil - bu yüzden hedef
// klasör olarak kullanıcının AppData'sı yerine SQL Server'ın KENDİ varsayılan yedek klasörü
// kullanılır (kurulumla gelir, servis hesabının zaten tam yetkisi vardır); aksi halde
// "Access denied" hatası alınır (ilk denemede tam olarak bu yaşandı).
public sealed class DatabaseBackupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DatabaseBackupService> _logger;
    private readonly IConfiguration _configuration;

    public DatabaseBackupService(IServiceScopeFactory scopeFactory, ILogger<DatabaseBackupService> logger, IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_configuration.GetValue("DatabaseBackup:Enabled", true))
        {
            _logger.LogInformation("Otomatik veritabanı yedekleme devre dışı.");
            return;
        }

        var intervalHours = _configuration.GetValue("DatabaseBackup:IntervalHours", 24);
        var retentionCount = _configuration.GetValue("DatabaseBackup:RetentionCount", 14);

        string? directory;
        using (var scope = _scopeFactory.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            directory = await ResolveDirectoryAsync(context, stoppingToken);
        }

        if (directory == null)
        {
            _logger.LogError("Yedekleme klasörü belirlenemedi - otomatik yedekleme devre dışı kalacak. appsettings.json \"DatabaseBackup:Directory\" ile SQL Server servis hesabının yazma yetkisi olan bir klasör verebilirsiniz.");
            return;
        }

        _logger.LogInformation("Otomatik veritabanı yedekleme aktif - klasör: {Directory}, sıklık: {IntervalHours} saat.", directory, intervalHours);

        while (!stoppingToken.IsCancellationRequested)
        {
            await TakeBackupAsync(directory, retentionCount, stoppingToken);

            try
            {
                await Task.Delay(TimeSpan.FromHours(intervalHours), stoppingToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    private async Task<string?> ResolveDirectoryAsync(ApplicationDbContext context, CancellationToken cancellationToken)
    {
        var configured = _configuration["DatabaseBackup:Directory"];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            try
            {
                Directory.CreateDirectory(configured);
                return configured;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Yapılandırılmış yedekleme klasörü oluşturulamadı ({Directory}), SQL Server'ın kendi varsayılan klasörüne düşülüyor.", configured);
            }
        }

        // xp_instance_regread: SSMS'in "Yedekle" iletişim kutusunun da kullandığı, SQL
        // Server'ın kurulumla gelen kendi BackupDirectory kayıt defteri değerini okuyan
        // standart yöntem - sysadmin/db_backupoperator yetkisi ister (BACKUP DATABASE zaten
        // ister), yerel geliştirmede Windows-trusted bağlantı genelde sysadmin'dir.
        try
        {
            var connection = context.Database.GetDbConnection();
            var shouldClose = connection.State != ConnectionState.Open;
            if (shouldClose)
            {
                await connection.OpenAsync(cancellationToken);
            }

            try
            {
                using var command = connection.CreateCommand();
                command.CommandText = @"
DECLARE @path NVARCHAR(500);
EXEC master.dbo.xp_instance_regread
    N'HKEY_LOCAL_MACHINE',
    N'Software\Microsoft\MSSQLServer\MSSQLServer',
    N'BackupDirectory',
    @path OUTPUT;
SELECT @path;";
                var result = await command.ExecuteScalarAsync(cancellationToken) as string;
                if (!string.IsNullOrWhiteSpace(result))
                {
                    return result;
                }
            }
            finally
            {
                if (shouldClose)
                {
                    await connection.CloseAsync();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SQL Server'ın varsayılan yedek klasörü okunamadı.");
        }

        return null;
    }

    private async Task TakeBackupAsync(string directory, int retentionCount, CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        try
        {
            var dbName = context.Database.GetDbConnection().Database;
            var fullPath = Path.Combine(directory, $"{dbName}_{DateTime.Now:yyyyMMdd_HHmmss}.bak");

            // dbName/fullPath kullanıcı girdisi değil (EF Core bağlantısı ve bu metodun kendi ürettiği değer), SQL injection riski yok.
            // COMPRESSION kasıtlı olarak YOK - SQL Server Express'te desteklenmiyor (sadece Standard/Enterprise'da var), ilk denemede tam olarak bu hata alındı.
            var sql = $"BACKUP DATABASE [{dbName}] TO DISK = @path WITH INIT, COPY_ONLY";
            await context.Database.ExecuteSqlRawAsync(sql, [new SqlParameter("@path", fullPath)], cancellationToken);

            _logger.LogInformation("Veritabanı yedeği alındı: {FullPath}", fullPath);
            await CleanupOldBackupsAsync(context, directory, dbName, retentionCount, cancellationToken);
        }
        catch (Exception ex)
        {
            // Yedekleme hatası uygulamayı çökertmemeli.
            _logger.LogError(ex, "Otomatik veritabanı yedeklemesi başarısız oldu.");
        }
    }

    // .NET süreci SQL Server'ın korumalı Backup klasörünü listeleyemiyor (yedek alma adımıyla
    // aynı izin engeli, ters yönde) - bu yüzden listeleme (xp_dirtree) ve silme (xp_delete_file)
    // de SQL Server motoru üzerinden yapılır. dbName önekiyle filtreleme, aynı klasörü paylaşan
    // başka bir veritabanının yedeğine asla dokunulmamasını garanti eder.
    private async Task CleanupOldBackupsAsync(ApplicationDbContext context, string directory, string dbName, int retentionCount, CancellationToken cancellationToken)
    {
        try
        {
            var connection = context.Database.GetDbConnection();
            var shouldClose = connection.State != ConnectionState.Open;
            if (shouldClose)
            {
                await connection.OpenAsync(cancellationToken);
            }

            try
            {
                var fileNames = new List<string>();
                using (var listCommand = connection.CreateCommand())
                {
                    listCommand.CommandText = "EXEC master.sys.xp_dirtree @path, 1, 1";
                    listCommand.Parameters.Add(new SqlParameter("@path", directory));

                    using var reader = await listCommand.ExecuteReaderAsync(cancellationToken);
                    while (await reader.ReadAsync(cancellationToken))
                    {
                        var isFile = Convert.ToInt32(reader.GetValue(2)) == 1;
                        if (isFile)
                        {
                            fileNames.Add(reader.GetString(0));
                        }
                    }
                }

                var prefix = dbName + "_";
                var oldFiles = fileNames
                    .Where(name => name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && name.EndsWith(".bak", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(name => name, StringComparer.OrdinalIgnoreCase)
                    .Skip(retentionCount);

                foreach (var name in oldFiles)
                {
                    using var deleteCommand = connection.CreateCommand();
                    deleteCommand.CommandText = "EXEC master.sys.xp_delete_file 0, @path, N'bak', @cutoff";
                    deleteCommand.Parameters.Add(new SqlParameter("@path", Path.Combine(directory, name)));
                    deleteCommand.Parameters.Add(new SqlParameter("@cutoff", DateTime.Now.AddYears(50)));
                    await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
                }
            }
            finally
            {
                if (shouldClose)
                {
                    await connection.CloseAsync();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Eski yedekler temizlenirken hata oluştu.");
        }
    }
}
