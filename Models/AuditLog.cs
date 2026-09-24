namespace RestoranYonetim.Models;

// Önemli işlemlerin (fiyat değişikliği, kullanıcı silme, ödeme onayı, adisyon iptali vb.) izi.
public class AuditLog
{
    public int Id { get; set; }

    public string? UserId { get; set; }
    public string? UserName { get; set; }

    public string Action { get; set; } = string.Empty;
    public string? EntityName { get; set; }
    public string? EntityId { get; set; }
    public string? Details { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
