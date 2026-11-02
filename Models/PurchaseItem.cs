using System.ComponentModel.DataAnnotations.Schema;

namespace RestoranYonetim.Models;

public class PurchaseItem
{
    public int Id { get; set; }

    public int PurchaseId { get; set; }
    public Purchase? Purchase { get; set; }

    public int InventoryItemId { get; set; }
    public InventoryItem? InventoryItem { get; set; }

    [Column(TypeName = "decimal(12,3)")]
    public decimal Quantity { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal UnitPrice { get; set; }

    [NotMapped]
    public decimal LineTotal => Quantity * UnitPrice;
}
