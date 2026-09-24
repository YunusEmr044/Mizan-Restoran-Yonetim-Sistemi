using System.ComponentModel.DataAnnotations.Schema;

namespace RestoranYonetim.Models;

// Stok hareket kaydı (giriş/çıkış geçmişi) - satın alma, satış düşümü, manuel düzeltme
// veya fire için oluşur. QuantityChange pozitifse giriş, negatifse çıkıştır.
public class InventoryTransaction
{
    public int Id { get; set; }

    public int InventoryItemId { get; set; }
    public InventoryItem? InventoryItem { get; set; }

    public InventoryTransactionType Type { get; set; }

    [Column(TypeName = "decimal(12,3)")]
    public decimal QuantityChange { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public string? Note { get; set; }
}
