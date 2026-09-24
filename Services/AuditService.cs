using RestoranYonetim.Data;
using RestoranYonetim.Models;

namespace RestoranYonetim.Services;

public class AuditService
{
    private readonly ApplicationDbContext _context;

    public AuditService(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task LogAsync(string? userId, string? userName, string action, string? entityName = null, string? entityId = null, string? details = null)
    {
        _context.AuditLogs.Add(new AuditLog
        {
            UserId = userId,
            UserName = userName,
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            Details = details
        });
        await _context.SaveChangesAsync();
    }
}
