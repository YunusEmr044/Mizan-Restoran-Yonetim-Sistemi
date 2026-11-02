using System.ComponentModel.DataAnnotations;

namespace RestoranYonetim.Models;

public class Category
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Kategori adı gereklidir.")]
    [StringLength(100)]
    [Display(Name = "Ad")]
    [Translatable]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Sıra")]
    public int DisplayOrder { get; set; }

    [Display(Name = "KDV oranı (%)")]
    [AllowedValues(0, 1, 10, 20, ErrorMessage = "KDV oranı 0, 1, 10 ya da 20 olmalı.")]
    public int VatRate { get; set; } = 10;

    public List<MenuItem> MenuItems { get; set; } = new();
}
