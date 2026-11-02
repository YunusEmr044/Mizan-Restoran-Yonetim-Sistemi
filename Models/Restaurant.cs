using System.ComponentModel.DataAnnotations;

namespace RestoranYonetim.Models;

// Sistemin en üst seviyesi; birden fazla restoranı destekleyecek şekilde tasarlandı.
public class Restaurant
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Restoran adı gereklidir.")]
    [StringLength(150)]
    [Display(Name = "Restoran Adı")]
    public string Name { get; set; } = string.Empty;

    [StringLength(300)]
    public string? LogoUrl { get; set; }

    [StringLength(50)]
    [Display(Name = "Telefon")]
    public string? Phone { get; set; }

    [StringLength(300)]
    [Display(Name = "Adres")]
    public string? Address { get; set; }

    public List<Branch> Branches { get; set; } = new();
}
