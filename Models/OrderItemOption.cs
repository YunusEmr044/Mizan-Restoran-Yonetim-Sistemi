using System.ComponentModel.DataAnnotations.Schema;

namespace RestoranYonetim.Models;

// ÖNEMLİ: Ad ve fiyat sipariş anında MenuItemOption'dan kopyalanır (snapshot) - menüdeki fiyat
// sonradan değişse bile geçmiş siparişler bozulmaz.
public class OrderItemOption
{
    public int Id { get; set; }

    public int OrderItemId { get; set; }
    public OrderItem? OrderItem { get; set; }

    public string Name { get; set; } = string.Empty;

    [Column(TypeName = "decimal(10,2)")]
    public decimal ExtraPrice { get; set; }
}
