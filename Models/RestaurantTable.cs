using System.ComponentModel.DataAnnotations;

namespace RestoranYonetim.Models;

public class RestaurantTable
{
    public int Id { get; set; }

    public int AreaId { get; set; }
    public Area? Area { get; set; }

    [Required(ErrorMessage = "Masa adı gereklidir.")]
    [StringLength(50)]
    [Display(Name = "Masa Adı")]
    public string Name { get; set; } = string.Empty;

    // QR kod bu token'a göre üretilir; yenilendiğinde eski token geçersiz kalır.
    public string QrToken { get; set; } = Guid.NewGuid().ToString("N");

    public TableStatus Status { get; set; } = TableStatus.Bos;

    public List<Order> Orders { get; set; } = new();
}
