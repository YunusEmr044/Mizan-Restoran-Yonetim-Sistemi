using System.ComponentModel.DataAnnotations;

namespace RestoranYonetim.Models;

// Herkese açık sitedeki rezervasyon formundan gelen talepler.
public class Reservation
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

    [Required(ErrorMessage = "Tarih gereklidir.")]
    [Display(Name = "Tarih")]
    [DataType(DataType.Date)]
    public DateTime Date { get; set; }

    [Required(ErrorMessage = "Saat gereklidir.")]
    [Display(Name = "Saat")]
    [DataType(DataType.Time)]
    public TimeSpan Time { get; set; }

    [Range(1, 100, ErrorMessage = "Geçerli bir kişi sayısı girin.")]
    [Display(Name = "Kişi Sayısı")]
    public int PartySize { get; set; } = 2;

    [StringLength(500)]
    [Display(Name = "Not")]
    public string? Note { get; set; }

    public ReservationStatus Status { get; set; } = ReservationStatus.Beklemede;

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
