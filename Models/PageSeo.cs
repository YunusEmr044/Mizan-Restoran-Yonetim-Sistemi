using System.ComponentModel.DataAnnotations;

namespace RestoranYonetim.Models;

// Sabit sayfalar için SAYFA BAZLI SEO ayarları. PageKey, Security/SeoPageCatalog.cs kataloğuna karşılık gelir.
// Tüm alanlar isteğe bağlıdır: boş bırakılırsa kod içi varsayılana, o da yoksa SeoSettings'e düşülür.
public class PageSeo
{
    public int Id { get; set; }

    [Required]
    [StringLength(40)]
    public string PageKey { get; set; } = string.Empty;

    [StringLength(70)]
    [Display(Name = "SEO Başlığı")]
    public string? Title { get; set; }

    [StringLength(300)]
    [Display(Name = "Meta Açıklama")]
    public string? MetaDescription { get; set; }

    [StringLength(300)]
    [Display(Name = "Canonical URL (boş bırakılırsa otomatik üretilir)")]
    public string? CanonicalUrl { get; set; }

    [Display(Name = "Arama Motorları Dizinlemesin (noindex)")]
    public bool NoIndex { get; set; }

    [Display(Name = "Sayfadaki Bağlantıları Takip Etme (nofollow)")]
    public bool NoFollow { get; set; }

    [StringLength(70)]
    [Display(Name = "Open Graph Başlığı (boşsa SEO Başlığı kullanılır)")]
    public string? OgTitle { get; set; }

    [StringLength(300)]
    [Display(Name = "Open Graph Açıklaması (boşsa Meta Açıklama kullanılır)")]
    public string? OgDescription { get; set; }

    [Display(Name = "Open Graph Görseli (boşsa site geneli varsayılan kullanılır)")]
    public string? OgImageUrl { get; set; }
}
