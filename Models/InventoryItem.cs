using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestoranYonetim.Models;

// Stok kalemi (hammadde/malzeme). Reçeteler (RecipeItem) üzerinden ürünlere bağlanır; satın almayla girer, satışta reçeteye göre düşer.
public class InventoryItem
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Malzeme adı gereklidir.")]
    [StringLength(150)]
    [Display(Name = "Ad")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Birim gereklidir.")]
    [StringLength(20)]
    [Display(Name = "Birim")]
    public string Unit { get; set; } = "adet";

    [Column(TypeName = "decimal(12,3)")]
    [Display(Name = "Mevcut Miktar")]
    public decimal CurrentQuantity { get; set; }

    [Column(TypeName = "decimal(12,3)")]
    [Display(Name = "Kritik Seviye")]
    public decimal MinLevel { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    [Display(Name = "Birim Alış Fiyatı")]
    public decimal UnitCost { get; set; }

    [Display(Name = "Tedarikçi")]
    public int? SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    [NotMapped]
    public bool IsLowStock => CurrentQuantity <= MinLevel;
}
