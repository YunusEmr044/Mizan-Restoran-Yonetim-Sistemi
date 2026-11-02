using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using RestoranYonetim.Data;

namespace RestoranYonetim.HealthChecks;

// Basit veritabanı bağlantı sağlık kontrolü - bir yük dengeleyici/orkestrasyon aracı uygulamanın
// veritabanına erişebildiğini buradan sorgulayabilir.
public class DatabaseHealthCheck : IHealthCheck
{
    private readonly ApplicationDbContext _context;

    public DatabaseHealthCheck(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var canConnect = await _context.Database.CanConnectAsync(cancellationToken);
            return canConnect
                ? HealthCheckResult.Healthy("Veritabanı bağlantısı çalışıyor.")
                : HealthCheckResult.Unhealthy("Veritabanına bağlanılamadı.");
        }
        catch (Exception ex)
        {
            // GÜVENLİK: Ayrıntılı istisna sadece sunucu loguna gider; /health uç noktası dışarı sade "Healthy"/"Unhealthy" döner, iç hata detayı sızmaz.
            return HealthCheckResult.Unhealthy("Veritabanı bağlantı hatası.", ex);
        }
    }
}
