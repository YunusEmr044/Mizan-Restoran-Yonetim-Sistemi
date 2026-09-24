using System.ComponentModel.DataAnnotations;

namespace RestoranYonetim.Models;

public class Category
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Kategori adı gereklidir.")]
    [StringLength(100)]
    [Display(Name = "Ad")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Sıra")]
    public int DisplayOrder { get; set; }

    public List<MenuItem> MenuItems { get; set; } = new();
}
