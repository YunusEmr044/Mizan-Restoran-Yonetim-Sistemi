using System.ComponentModel.DataAnnotations.Schema;

namespace RestoranYonetim.Models;

public class Order
{
    public int Id { get; set; }

    public int TableId { get; set; }
    public RestaurantTable? Table { get; set; }

    // Sipariş gönderildiğinde (taslak onaylandığında) masanın aktif adisyonuna bağlanır.
    // Taslak aşamasında null olabilir.
    public int? AdisyonId { get; set; }
    public Adisyon? Adisyon { get; set; }

    // Taslak: müşteri henüz onaylamadı / garson henüz göndermedi. Taslak siparişler
    // mutfak/bar ekranında görünmez ve adisyon tutarına dahil edilmez.
    public bool IsDraft { get; set; } = true;

    public OrderSource Source { get; set; } = OrderSource.Musteri;

    // Kalemlerden otomatik hesaplanan özet durum (bkz. OrderStatusHelper.Recompute).
    public OrderStatus Status { get; set; } = OrderStatus.Yeni;

    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime? SentAt { get; set; }

    public string? Notes { get; set; }

    public string? CreatedByUserId { get; set; }
    public string? CreatedByUserName { get; set; }

    public List<OrderItem> Items { get; set; } = new();

    [NotMapped]
    public decimal Total => Items.Where(i => i.Status != OrderStatus.IptalEdildi).Sum(i => i.LineTotal);
}
