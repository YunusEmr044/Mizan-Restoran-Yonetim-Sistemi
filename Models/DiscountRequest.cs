using System.ComponentModel.DataAnnotations.Schema;

namespace RestoranYonetim.Models;

// Garsonun bir adisyona indirim/ikram uygulanması için oluşturduğu talep. Müdür (veya
// üstü yetkili) onaylayınca tutar adisyona (DiscountAmount) yansır.
public class DiscountRequest
{
    public int Id { get; set; }

    public int AdisyonId { get; set; }
    public Adisyon? Adisyon { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal Amount { get; set; }

    public string? Reason { get; set; }

    public string RequestedByUserId { get; set; } = string.Empty;
    public string? RequestedByUserName { get; set; }

    public DiscountRequestStatus Status { get; set; } = DiscountRequestStatus.Beklemede;

    public string? DecidedByUserName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? DecidedAt { get; set; }
}
