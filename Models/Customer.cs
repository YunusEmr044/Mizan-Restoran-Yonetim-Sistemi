using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestoranYonetim.Models;

// Hafif müşteri/sadakat kaydı - ayrı bir giriş sistemi yok; telefon numarasıyla eşleştirilip puan/harcama biriktirilir.
public class Customer
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Ad soyad gereklidir.")]
    [StringLength(150)]
    [Display(Name = "Ad Soyad")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Telefon gereklidir.")]
    [StringLength(50)]
    [Display(Name = "Telefon")]
    public string Phone { get; set; } = string.Empty;

    [StringLength(150)]
    [Display(Name = "E-posta")]
    public string? Email { get; set; }

    [Display(Name = "Puan")]
    public int Points { get; set; }

    [Column(TypeName = "decimal(12,2)")]
    [Display(Name = "Toplam Harcama")]
    public decimal TotalSpent { get; set; }

    [Display(Name = "Seviye")]
    public CustomerLevel Level { get; set; } = CustomerLevel.Standart;

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
