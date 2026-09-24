using System.ComponentModel.DataAnnotations;

namespace RestoranYonetim.Models;

// SEO/AEO/GEO için site geneli tekil ayar kaydı. Restoran adı/adres/telefon gibi bilgiler burada
// TEKRARLANMAZ; SiteContent tek doğru kaynaktır (single source of truth).
public class SeoSettings
{
    public int Id { get; set; }

    [StringLength(300)]
    [Display(Name = "Varsayılan Meta Açıklama")]
    public string? DefaultMetaDescription { get; set; }

    [Display(Name = "Varsayılan Paylaşım Görseli (Open Graph)")]
    public string? DefaultOgImageUrl { get; set; }

    [Display(Name = "Favicon")]
    public string? FaviconUrl { get; set; }

    // Kapalıysa TÜM sayfalar noindex olur; sayfa bazlı NoIndex (PageSeo) bundan bağımsız ayrıca çalışır.
    [Display(Name = "Arama Motorlarında Görünsün (Site Geneli)")]
    public bool IndexingEnabled { get; set; } = true;

    [Display(Name = "Site Haritası (sitemap.xml) Aktif")]
    public bool SitemapEnabled { get; set; } = true;

    // robots.txt'ye olduğu gibi eklenir; temel Disallow/Sitemap satırları zaten otomatik ekleniyor.
    [Display(Name = "robots.txt Ek Kuralları")]
    public string? RobotsExtraRules { get; set; }

    [StringLength(100)]
    [Display(Name = "Google Site Doğrulama Kodu (meta tag içeriği)")]
    public string? GoogleSiteVerification { get; set; }

    [StringLength(100)]
    [Display(Name = "Bing Site Doğrulama Kodu (meta tag içeriği)")]
    public string? BingSiteVerification { get; set; }

    [StringLength(60)]
    [Display(Name = "Twitter/X Kullanıcı Adı (@ olmadan)")]
    public string? TwitterHandle { get; set; }

    [StringLength(40)]
    [Display(Name = "İşletme Türü (Schema.org)")]
    public string LocalBusinessType { get; set; } = "Restaurant";

    // Boş bırakılırsa yapılandırılmış veride hiç üretilmez.
    [StringLength(10)]
    [Display(Name = "Fiyat Aralığı (ör. ₺₺)")]
    public string? PriceRange { get; set; }
}
