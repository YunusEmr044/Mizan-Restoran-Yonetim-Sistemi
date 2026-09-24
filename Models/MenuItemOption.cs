using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestoranYonetim.Models;

// Bir ürüne eklenebilecek ekstra seçenek (örn. "Ekstra Peynir") veya çıkarılabilecek malzeme bilgisi.
public class MenuItemOption
{
    public int Id { get; set; }

    public int MenuItemId { get; set; }
    public MenuItem? MenuItem { get; set; }

    [Required(ErrorMessage = "Seçenek adı gereklidir.")]
    [StringLength(100)]
    [Display(Name = "Seçenek Adı")]
    public string Name { get; set; } = string.Empty;

    [Column(TypeName = "decimal(10,2)")]
    [Display(Name = "Ek Fiyat")]
    public decimal ExtraPrice { get; set; }

    [Display(Name = "Aktif")]
    public bool IsAvailable { get; set; } = true;
}
