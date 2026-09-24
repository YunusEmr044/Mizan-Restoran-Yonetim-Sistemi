using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestoranYonetim.Models;

// Bir masanın aktif hesabı. Bir masada aynı anda en fazla bir "açık" adisyon olur.
// Kapatma (Ödendi) işlemi sadece kasa/yönetim yetkisiyle yapılabilir, garson doğrudan kapatamaz.
public class Adisyon
{
    public int Id { get; set; }

    public int TableId { get; set; }
    public RestaurantTable? Table { get; set; }

    public AdisyonStatus Status { get; set; } = AdisyonStatus.Acik;

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? ClosedAt { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal DiscountAmount { get; set; }
    public string? DiscountReason { get; set; }

    // ÖNEMLİ: Optimistic concurrency denetimi (SQL "rowversion"). Aynı adisyon eşzamanlı değiştirilirse
    // (ör. iki kasiyer aynı hesabı aynı anda kapatmaya çalışırsa) EF DbUpdateConcurrencyException fırlatır;
    // parasal veri "son yazan kazanır" ile sessizce bozulmaz.
    [Timestamp]
    public byte[] RowVersion { get; set; } = null!;

    public List<Order> Orders { get; set; } = new();
    public List<Payment> Payments { get; set; } = new();
    public List<DiscountRequest> DiscountRequests { get; set; } = new();

    // Taslak (henüz gönderilmemiş) siparişler toplam tutara dahil edilmez - müşteri/
    // garson onaylayıp göndermeden fiyat hesaba yansımaz.
    [NotMapped]
    public decimal ItemsTotal => Orders.Where(o => !o.IsDraft && o.Status != OrderStatus.IptalEdildi).Sum(o => o.Total);

    [NotMapped]
    public decimal GrandTotal => Math.Max(0, ItemsTotal - DiscountAmount);

    [NotMapped]
    public decimal PaidTotal => Payments.Sum(p => p.Amount);

    [NotMapped]
    public decimal RemainingTotal => Math.Max(0, GrandTotal - PaidTotal);
}
