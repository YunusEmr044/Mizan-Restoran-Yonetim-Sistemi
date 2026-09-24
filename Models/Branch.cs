using System.ComponentModel.DataAnnotations;

namespace RestoranYonetim.Models;

// Bir restoranın şubesi (örn. "Siirt Şubesi", "Batman Şubesi").
public class Branch
{
    public int Id { get; set; }

    public int RestaurantId { get; set; }
    public Restaurant? Restaurant { get; set; }

    [Required(ErrorMessage = "Şube adı gereklidir.")]
    [StringLength(150)]
    [Display(Name = "Şube Adı")]
    public string Name { get; set; } = string.Empty;

    [StringLength(300)]
    [Display(Name = "Adres")]
    public string? Address { get; set; }

    [StringLength(50)]
    [Display(Name = "Telefon")]
    public string? Phone { get; set; }

    public List<Area> Areas { get; set; } = new();
}
