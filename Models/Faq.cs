using System.ComponentModel.DataAnnotations;

namespace RestoranYonetim.Models;

// SSS (Sıkça Sorulan Sorular) - AEO için. Yayınlanan (IsActive) kayıtlardan hem public SSS sayfası
// hem de FAQPage yapılandırılmış verisi otomatik üretilir.
public class Faq
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Soru gereklidir.")]
    [StringLength(300)]
    [Display(Name = "Soru")]
    public string Question { get; set; } = string.Empty;

    [Required(ErrorMessage = "Cevap gereklidir.")]
    [StringLength(2000)]
    [Display(Name = "Cevap")]
    public string Answer { get; set; } = string.Empty;

    // Boş bırakılırsa "Genel" altında gösterilir.
    [StringLength(60)]
    [Display(Name = "Kategori")]
    public string? Category { get; set; }

    [Display(Name = "Sıra")]
    public int DisplayOrder { get; set; }

    [Display(Name = "Aktif (yayında)")]
    public bool IsActive { get; set; } = true;
}
