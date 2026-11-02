using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestoranYonetim.Models;

public class MenuItem
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Ürün adı gereklidir.")]
    [StringLength(150)]
    [Display(Name = "Ad")]
    [Translatable]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    [Display(Name = "Açıklama")]
    [Translatable]
    public string? Description { get; set; }

    [Range(0, 100000, ErrorMessage = "Geçerli bir fiyat girin.")]
    [Column(TypeName = "decimal(10,2)")]
    [Display(Name = "Fiyat")]
    public decimal Price { get; set; }

    // Boşsa kategorinin KDV oranı kullanılır.
    [Display(Name = "KDV oranı (%)")]
    [AllowedValues(null, 0, 1, 10, 20, ErrorMessage = "KDV oranı 0, 1, 10 ya da 20 olmalı.")]
    public int? VatRate { get; set; }

    [Display(Name = "Menüde Görünsün")]
    public bool IsAvailable { get; set; } = true;

    [Display(Name = "Bölüm")]
    public MenuDepartment Department { get; set; } = MenuDepartment.Mutfak;

    public string? ImageUrl { get; set; }

    [Display(Name = "Ana Sayfada Öne Çıkar")]
    public bool IsFeaturedHome { get; set; }

    [Display(Name = "Ana Sayfa Sırası")]
    public int FeaturedOrder { get; set; }

    [Display(Name = "Kategori")]
    public int CategoryId { get; set; }
    public Category? Category { get; set; }

    // Reçete tanımlanmamışsa tahmini maliyet 0 kabul edilir.
    public List<RecipeItem> RecipeItems { get; set; } = new();

    [NotMapped]
    public decimal EstimatedCost => RecipeItems.Sum(r => r.Quantity * (r.InventoryItem?.UnitCost ?? 0));

    [NotMapped]
    public decimal EstimatedProfit => Price - EstimatedCost;
}
