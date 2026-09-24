using System.ComponentModel.DataAnnotations.Schema;

namespace RestoranYonetim.Models;

public class OrderItem
{
    public int Id { get; set; }

    public int OrderId { get; set; }
    public Order? Order { get; set; }

    public int MenuItemId { get; set; }
    public MenuItem? MenuItem { get; set; }

    // Sipariş anındaki ürün adı - menüden silinse/değişse bile sipariş geçmişi bozulmasın diye ayrıca tutulur.
    public string MenuItemName { get; set; } = string.Empty;

    [Column(TypeName = "decimal(10,2)")]
    public decimal UnitPrice { get; set; }

    public int Quantity { get; set; } = 1;

    public string? Notes { get; set; }

    // Mutfak/bar ekranının asıl çalıştığı, kalem bazlı gerçek durum.
    public OrderStatus Status { get; set; } = OrderStatus.Yeni;

    public List<OrderItemOption> Options { get; set; } = new();

    [NotMapped]
    public decimal OptionsUnitPrice => Options.Sum(o => o.ExtraPrice);

    [NotMapped]
    public decimal LineTotal => (UnitPrice + OptionsUnitPrice) * Quantity;
}
