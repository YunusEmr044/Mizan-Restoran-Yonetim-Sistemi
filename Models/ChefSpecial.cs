using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestoranYonetim.Models;

// Ana sayfa "Şefin Önerisi" alanı: menüden bir ürün seçilir, başlık/açıklama/görsel/fiyat isteğe bağlı
// override edilebilir (boşsa MenuItem'ın kendi bilgisi kullanılır). Tarih aralığı dışına çıkanlar otomatik yayından kalkar.
public class ChefSpecial
{
    public int Id { get; set; }

    [Display(Name = "Menü Ürünü")]
    public int MenuItemId { get; set; }
    public MenuItem? MenuItem { get; set; }

    [StringLength(150)]
    [Display(Name = "Başlık (boş bırakılırsa ürün adı kullanılır)")]
    public string? TitleOverride { get; set; }

    [StringLength(500)]
    [Display(Name = "Açıklama (boş bırakılırsa ürün açıklaması kullanılır)")]
    public string? DescriptionOverride { get; set; }

    [Display(Name = "Görsel (boş bırakılırsa ürün görseli kullanılır)")]
    public string? ImageUrlOverride { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    [Display(Name = "Fiyat (boş bırakılırsa ürün fiyatı kullanılır)")]
    public decimal? PriceOverride { get; set; }

    [Display(Name = "Başlangıç Tarihi")]
    public DateTime StartDate { get; set; } = DateTime.Today;

    [Display(Name = "Bitiş Tarihi")]
    public DateTime EndDate { get; set; } = DateTime.Today.AddDays(7);

    [Display(Name = "Aktif")]
    public bool IsActive { get; set; } = true;

    [NotMapped]
    public bool IsCurrentlyRunning => IsActive && DateTime.Today >= StartDate.Date && DateTime.Today <= EndDate.Date;

    [NotMapped]
    public string EffectiveTitle => !string.IsNullOrWhiteSpace(TitleOverride) ? TitleOverride! : (MenuItem?.Name ?? string.Empty);

    [NotMapped]
    public string? EffectiveDescription => !string.IsNullOrWhiteSpace(DescriptionOverride) ? DescriptionOverride : MenuItem?.Description;

    [NotMapped]
    public string? EffectiveImageUrl => !string.IsNullOrWhiteSpace(ImageUrlOverride) ? ImageUrlOverride : MenuItem?.ImageUrl;

    [NotMapped]
    public decimal EffectivePrice => PriceOverride ?? MenuItem?.Price ?? 0m;
}
