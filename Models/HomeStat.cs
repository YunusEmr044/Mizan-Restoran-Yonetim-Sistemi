using System.ComponentModel.DataAnnotations;

namespace RestoranYonetim.Models;

// Ana sayfa "güven ve istatistik" alanı. Value bilinçli olarak serbest metin (sayısal tipe zorlanmadı, ör. "4.9 / 5", "50+").
public class HomeStat
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Değer gereklidir.")]
    [StringLength(30)]
    [Display(Name = "Değer")]
    public string Value { get; set; } = string.Empty;

    [Required(ErrorMessage = "Etiket gereklidir.")]
    [StringLength(100)]
    [Display(Name = "Etiket")]
    public string Label { get; set; } = string.Empty;

    [StringLength(40)]
    [Display(Name = "İkon (Bootstrap Icons sınıfı, ör. bi-star-fill)")]
    public string? Icon { get; set; }

    [Display(Name = "Sıra")]
    public int DisplayOrder { get; set; }

    [Display(Name = "Aktif")]
    public bool IsActive { get; set; } = true;
}
