using System.ComponentModel.DataAnnotations;

namespace RestoranYonetim.Models;

// Ana sayfa Hero alanı; admin panelinden yönetilebilen birden fazla slayt destekler.
// SiteContent'teki eski tekil Hero* alanları geriye dönük uyumluluk için korunur (hiç slayt yoksa/hepsi pasifse oraya düşülür).
public class HeroSlide
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Başlık gereklidir.")]
    [StringLength(150)]
    [Display(Name = "Başlık")]
    [Translatable]
    public string Title { get; set; } = string.Empty;

    [StringLength(300)]
    [Display(Name = "Alt Başlık / Slogan")]
    [Translatable]
    public string? Subtitle { get; set; }

    [StringLength(500)]
    [Display(Name = "Kısa Açıklama")]
    [Translatable]
    public string? Description { get; set; }

    [Display(Name = "Görsel (Masaüstü)")]
    public string? ImageUrl { get; set; }

    [Display(Name = "Görsel (Mobil)")]
    public string? MobileImageUrl { get; set; }

    [StringLength(80)]
    [Display(Name = "Ana Buton Metni")]
    [Translatable]
    public string? PrimaryButtonText { get; set; }

    [StringLength(300)]
    [Display(Name = "Ana Buton Linki")]
    public string? PrimaryButtonUrl { get; set; }

    [StringLength(80)]
    [Display(Name = "İkinci Buton Metni")]
    [Translatable]
    public string? SecondaryButtonText { get; set; }

    [StringLength(300)]
    [Display(Name = "İkinci Buton Linki")]
    public string? SecondaryButtonUrl { get; set; }

    [Display(Name = "Sıra")]
    public int DisplayOrder { get; set; }

    [Display(Name = "Aktif")]
    public bool IsActive { get; set; } = true;
}
