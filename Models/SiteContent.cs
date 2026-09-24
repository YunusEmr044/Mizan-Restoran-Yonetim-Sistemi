using System.ComponentModel.DataAnnotations;

namespace RestoranYonetim.Models;

// Herkese açık restoran sitesinin düzenlenebilir içeriği. Tekil bir kayıt olarak tutulur.
public class SiteContent
{
    public int Id { get; set; }

    // Ana sayfa
    [StringLength(150)]
    [Display(Name = "Ana Başlık")]
    public string HeroTitle { get; set; } = "Restoranımıza Hoş Geldiniz";

    [StringLength(300)]
    [Display(Name = "Alt Başlık / Slogan")]
    public string? HeroSubtitle { get; set; }

    [Display(Name = "Kapak Görseli")]
    public string? HeroImageUrl { get; set; }

    [StringLength(80)]
    [Display(Name = "Ana Buton Metni")]
    public string? HeroPrimaryButtonText { get; set; }

    [StringLength(300)]
    [Display(Name = "Ana Buton Linki")]
    public string? HeroPrimaryButtonUrl { get; set; }

    [StringLength(80)]
    [Display(Name = "İkinci Buton Metni")]
    public string? HeroSecondaryButtonText { get; set; }

    [StringLength(300)]
    [Display(Name = "İkinci Buton Linki")]
    public string? HeroSecondaryButtonUrl { get; set; }

    // Hakkımızda
    [StringLength(150)]
    [Display(Name = "Hakkımızda Başlığı")]
    public string AboutTitle { get; set; } = "Hakkımızda";

    [Display(Name = "Hakkımızda Metni")]
    public string? AboutText { get; set; }

    [Display(Name = "Hakkımızda Görseli")]
    public string? AboutImageUrl { get; set; }

    [Display(Name = "Logo")]
    public string? LogoImageUrl { get; set; }

    // İletişim / Konum
    [StringLength(50)]
    [Display(Name = "Telefon")]
    public string? ContactPhone { get; set; }

    [StringLength(150)]
    [Display(Name = "E-posta")]
    public string? ContactEmail { get; set; }

    [StringLength(300)]
    [Display(Name = "Adres")]
    public string? ContactAddress { get; set; }

    [Display(Name = "Harita Gömme Linki (Google Maps Embed URL)")]
    public string? MapEmbedUrl { get; set; }

    [StringLength(300)]
    [Display(Name = "Facebook Linki")]
    public string? FacebookUrl { get; set; }

    [StringLength(300)]
    [Display(Name = "Instagram Linki")]
    public string? InstagramUrl { get; set; }

    [StringLength(30)]
    [Display(Name = "WhatsApp Numarası")]
    public string? WhatsAppNumber { get; set; }

    [Display(Name = "Çalışma Saatleri")]
    public string? WorkingHours { get; set; }

    [StringLength(200)]
    [Display(Name = "SEO Başlığı")]
    public string? SeoTitle { get; set; }

    [StringLength(500)]
    [Display(Name = "SEO Açıklaması")]
    public string? SeoDescription { get; set; }

    [Display(Name = "Footer Metni")]
    public string? FooterText { get; set; }

    // Rezervasyon
    [Display(Name = "Rezervasyon Formu Açık")]
    public bool ReservationEnabled { get; set; } = true;

    // Boş bırakılırsa view tarafında varsayılan Türkçe metne düşülür.
    [StringLength(150)]
    [Display(Name = "Özellikler Bölümü Başlığı")]
    public string? FeaturesSectionTitle { get; set; }

    [StringLength(300)]
    [Display(Name = "Özellikler Bölümü Alt Başlığı")]
    public string? FeaturesSectionSubtitle { get; set; }

    [StringLength(150)]
    [Display(Name = "Öne Çıkan Lezzetler Başlığı")]
    public string? FeaturedMenuSectionTitle { get; set; }

    [StringLength(300)]
    [Display(Name = "Öne Çıkan Lezzetler Alt Başlığı")]
    public string? FeaturedMenuSectionSubtitle { get; set; }

    [StringLength(80)]
    [Display(Name = "Hikâyemiz Bölümü Buton Metni")]
    public string? AboutButtonText { get; set; }

    [StringLength(150)]
    [Display(Name = "Şefin Önerisi Bölümü Başlığı")]
    public string? ChefSpecialSectionTitle { get; set; }

    [StringLength(300)]
    [Display(Name = "Şefin Önerisi Bölümü Alt Başlığı")]
    public string? ChefSpecialSectionSubtitle { get; set; }

    [StringLength(150)]
    [Display(Name = "Kampanyalar Bölümü Başlığı")]
    public string? CampaignsSectionTitle { get; set; }

    [StringLength(150)]
    [Display(Name = "Yorumlar Bölümü Başlığı")]
    public string? TestimonialsSectionTitle { get; set; }

    [StringLength(150)]
    [Display(Name = "Galeri Bölümü Başlığı")]
    public string? GallerySectionTitle { get; set; }

    [StringLength(150)]
    [Display(Name = "QR Menü Bölümü Başlığı")]
    public string? QrSectionTitle { get; set; }

    [StringLength(300)]
    [Display(Name = "QR Menü Bölümü Alt Başlığı")]
    public string? QrSectionSubtitle { get; set; }

    // Ana sayfada gerçek bir QR kod gösterilmez (her masanın kendi QR kodu var); bunun yerine 3 adımlı akış anlatılır.
    [StringLength(80)]
    [Display(Name = "1. Adım Başlığı")]
    public string? QrStep1Title { get; set; }

    [StringLength(200)]
    [Display(Name = "1. Adım Açıklaması")]
    public string? QrStep1Text { get; set; }

    [StringLength(80)]
    [Display(Name = "2. Adım Başlığı")]
    public string? QrStep2Title { get; set; }

    [StringLength(200)]
    [Display(Name = "2. Adım Açıklaması")]
    public string? QrStep2Text { get; set; }

    [StringLength(80)]
    [Display(Name = "3. Adım Başlığı")]
    public string? QrStep3Title { get; set; }

    [StringLength(200)]
    [Display(Name = "3. Adım Açıklaması")]
    public string? QrStep3Text { get; set; }

    [Display(Name = "Telefon Mockup Görseli (örnek menü ekran görüntüsü)")]
    public string? QrPhoneImageUrl { get; set; }

    [StringLength(150)]
    [Display(Name = "Rezervasyon Çağrısı Başlığı")]
    public string? ReservationCtaTitle { get; set; }

    [StringLength(300)]
    [Display(Name = "Rezervasyon Çağrısı Açıklaması")]
    public string? ReservationCtaText { get; set; }

    [StringLength(80)]
    [Display(Name = "Rezervasyon Çağrısı Buton Metni")]
    public string? ReservationCtaButtonText { get; set; }

    [Display(Name = "Rezervasyon Çağrısı Arka Plan Görseli")]
    public string? ReservationCtaImageUrl { get; set; }

    [StringLength(150)]
    [Display(Name = "Instagram Bölümü Başlığı")]
    public string? InstagramSectionTitle { get; set; }

    [StringLength(60)]
    [Display(Name = "Instagram Kullanıcı Adı (@ olmadan)")]
    public string? InstagramHandle { get; set; }

    [StringLength(300)]
    [Display(Name = "WhatsApp Varsayılan Mesajı")]
    public string? WhatsAppDefaultMessage { get; set; }

    [StringLength(300)]
    [Display(Name = "Footer Kısa Açıklama")]
    public string? FooterTagline { get; set; }

    // ---- Görünüm / Tema (herkese açık site) ----
    // Okunurken her zaman ThemeCatalog.Safe*() ile doğrulanır ki boş/bozuk bir değer siteyi bozmasın.
    [StringLength(7)]
    [Display(Name = "Ana Vurgu Rengi")]
    public string ThemePrimaryColor { get; set; } = ThemeCatalog.DefaultPrimary;

    [StringLength(7)]
    [Display(Name = "Koyu Zemin Rengi")]
    public string ThemeDarkColor { get; set; } = ThemeCatalog.DefaultDark;

    [StringLength(7)]
    [Display(Name = "Sayfa Zemin Rengi")]
    public string ThemeBackgroundColor { get; set; } = ThemeCatalog.DefaultBackground;

    [StringLength(7)]
    [Display(Name = "Yazı Rengi")]
    public string ThemeTextColor { get; set; } = ThemeCatalog.DefaultText;

    [StringLength(40)]
    [Display(Name = "Başlık Fontu")]
    public string ThemeHeadingFont { get; set; } = ThemeCatalog.DefaultHeadingFont;

    [StringLength(40)]
    [Display(Name = "Metin Fontu")]
    public string ThemeBodyFont { get; set; } = ThemeCatalog.DefaultBodyFont;

    [StringLength(20)]
    [Display(Name = "Köşe Stili")]
    public string ThemeCornerStyle { get; set; } = ThemeCatalog.DefaultCornerStyle;

    // ---- Görünüm / Tema (yönetim paneli) ----
    [StringLength(7)]
    [Display(Name = "Panel Vurgu Rengi")]
    public string AdminAccentColor { get; set; } = ThemeCatalog.DefaultAdminAccent;

    [StringLength(7)]
    [Display(Name = "Panel Menü (Sidebar) Rengi")]
    public string AdminSidebarColor { get; set; } = ThemeCatalog.DefaultAdminSidebar;

    [StringLength(7)]
    [Display(Name = "Panel Zemin Rengi")]
    public string AdminBackgroundColor { get; set; } = ThemeCatalog.DefaultAdminBackground;

    // QR kodlarının ve site linklerinin kullanacağı adres (Ayarlar ekranı). Boşsa appsettings "PublicBaseUrl" kullanılır.
    [StringLength(200)]
    [Display(Name = "Site Adresi")]
    public string? PublicBaseUrl { get; set; }
}
