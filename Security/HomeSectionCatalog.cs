namespace RestoranYonetim.Security;

// Footer kasıtlı olarak burada yok - her zaman gösterilir, kapatılamaz.
public sealed record HomeSectionDefinition(string Key, string Label, int DefaultOrder);

public static class HomeSectionCatalog
{
    public const string Hero = "hero";
    public const string Stats = "stats";
    public const string Features = "features";
    public const string FeaturedMenu = "featured-menu";
    public const string Story = "story";
    public const string ChefSpecial = "chef-special";
    public const string Campaigns = "campaigns";
    public const string Testimonials = "testimonials";
    public const string Gallery = "gallery";
    public const string Qr = "qr";
    public const string Reservation = "reservation";
    public const string Contact = "contact";
    public const string Instagram = "instagram";

    public static IReadOnlyList<HomeSectionDefinition> All { get; } =
    [
        new(Hero, "Hero (Karşılama Alanı)", 1),
        new(Stats, "Güven / İstatistik Şeridi", 2),
        new(Features, "Öne Çıkan Özellikler", 3),
        new(FeaturedMenu, "Öne Çıkan Lezzetler", 4),
        new(Story, "Hikâyemiz", 5),
        new(ChefSpecial, "Şefin Önerisi", 6),
        new(Campaigns, "Kampanyalar", 7),
        new(Testimonials, "Müşteri Yorumları", 8),
        new(Gallery, "Galeri", 9),
        new(Qr, "QR Menü Alanı", 10),
        new(Reservation, "Rezervasyon Çağrısı", 11),
        new(Contact, "Konum ve İletişim", 12),
        new(Instagram, "Instagram / Sosyal Medya", 13)
    ];
}
