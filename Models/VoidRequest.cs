using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestoranYonetim.Models;

// Kalem/ödeme/adisyon iptali ve iade talebi. Kasiyer veya garson açar, "İade/İptal Onaylama" izni olan (Müdür ve üstü) onaylar.
public class VoidRequest
{
    public int Id { get; set; }

    public VoidRequestType Type { get; set; }

    public int AdisyonId { get; set; }
    public Adisyon? Adisyon { get; set; }

    public int? OrderItemId { get; set; }
    public OrderItem? OrderItem { get; set; }

    public int? PaymentId { get; set; }
    public Payment? Payment { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal? Amount { get; set; }

    public PaymentMethod? RefundMethod { get; set; }

    [Required(ErrorMessage = "Gerekçe gereklidir.")]
    [StringLength(300)]
    public string Reason { get; set; } = string.Empty;

    public string RequestedByUserId { get; set; } = string.Empty;
    public string? RequestedByUserName { get; set; }

    public DiscountRequestStatus Status { get; set; } = DiscountRequestStatus.Beklemede;
    public string? DecidedByUserName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? DecidedAt { get; set; }
}
