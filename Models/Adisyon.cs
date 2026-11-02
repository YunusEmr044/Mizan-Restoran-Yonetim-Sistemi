using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestoranYonetim.Models;

// Bir masanın aktif hesabı. Bir masada aynı anda en fazla bir "açık" adisyon olur.
// Kapatma (Ödendi) işlemi sadece kasa/yönetim yetkisiyle yapılabilir, garson doğrudan kapatamaz.
public class Adisyon
{
    public int Id { get; set; }

    // Paket (gel-al, adrese teslim) adisyonlarında masa yoktur.
    public int? TableId { get; set; }
    public RestaurantTable? Table { get; set; }

    // Paket adisyonlarının şubesi; masa adisyonlarında şube masanın alanından gelir.
    public int? BranchId { get; set; }
    public Branch? Branch { get; set; }

    public AdisyonStatus Status { get; set; } = AdisyonStatus.Acik;

    public OrderType OrderType { get; set; } = OrderType.Masa;

    public PackageInfo? Package { get; set; }

    // Birleştirilen adisyonun siparişlerinin aktarıldığı adisyon.
    public int? MergedIntoAdisyonId { get; set; }

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

    // Masada açık sayılan (ödemesi bitmemiş) adisyon durumları.
    public static readonly AdisyonStatus[] ActiveStatuses = { AdisyonStatus.Acik, AdisyonStatus.HesapIstendi, AdisyonStatus.Kasada, AdisyonStatus.KismiOdendi };

    [NotMapped]
    public bool IsClosed => Status is AdisyonStatus.Odendi or AdisyonStatus.Iptal or AdisyonStatus.Iade or AdisyonStatus.Birlestirildi;
}
