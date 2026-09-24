using System.ComponentModel.DataAnnotations;

namespace RestoranYonetim.Models;

// Bir şubenin içindeki alan (örn. "Salon", "Bahçe", "Teras"). Masalar bir alana bağlıdır.
public class Area
{
    public int Id { get; set; }

    public int BranchId { get; set; }
    public Branch? Branch { get; set; }

    [Required(ErrorMessage = "Alan adı gereklidir.")]
    [StringLength(100)]
    [Display(Name = "Alan Adı")]
    public string Name { get; set; } = string.Empty;

    public List<RestaurantTable> Tables { get; set; } = new();
}
