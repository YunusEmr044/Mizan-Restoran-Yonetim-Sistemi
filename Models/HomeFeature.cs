using System.ComponentModel.DataAnnotations;

namespace RestoranYonetim.Models;

// Ana sayfa "öne çıkan özellikler" alanı; admin panelinden yönetilir.
public class HomeFeature
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Başlık gereklidir.")]
    [StringLength(100)]
    [Display(Name = "Başlık")]
    public string Title { get; set; } = string.Empty;

    [StringLength(300)]
    [Display(Name = "Açıklama")]
    public string? Description { get; set; }

    [StringLength(40)]
    [Display(Name = "İkon (Bootstrap Icons sınıfı, ör. bi-egg-fried)")]
    public string? Icon { get; set; }

    [Display(Name = "Sıra")]
    public int DisplayOrder { get; set; }

    [Display(Name = "Aktif")]
    public bool IsActive { get; set; } = true;
}
