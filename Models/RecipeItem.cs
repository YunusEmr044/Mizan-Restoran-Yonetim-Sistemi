using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestoranYonetim.Models;

// Bir menü ürününün reçetesindeki tek bir malzeme satırı (ürün -> malzeme + miktar eşlemesi).
public class RecipeItem
{
    public int Id { get; set; }

    public int MenuItemId { get; set; }
    public MenuItem? MenuItem { get; set; }

    [Display(Name = "Malzeme")]
    public int InventoryItemId { get; set; }
    public InventoryItem? InventoryItem { get; set; }

    [Column(TypeName = "decimal(12,3)")]
    [Display(Name = "Miktar")]
    public decimal Quantity { get; set; }
}
