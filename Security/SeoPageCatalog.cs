namespace RestoranYonetim.Security;

// Path buradaki KANONİK (temiz) URL'dir - canonical link ve sitemap.xml üretimi bunu kullanır.
public sealed record SeoPageDefinition(string Key, string Label, string Path, string DefaultTitle, string? DefaultDescription);

public static class SeoPageCatalog
{
    public const string Home = "home";
    public const string Menu = "menu";
    public const string About = "about";
    public const string Gallery = "gallery";
    public const string Contact = "contact";
    public const string Reservation = "reservation";
    public const string Faq = "faq";

    public static IReadOnlyList<SeoPageDefinition> All { get; } =
    [
        new(Home, "Ana Sayfa", "/", "Ana Sayfa", null),
        new(Menu, "Menü", "/menu", "Menü", "Güncel menümüzü, ürünlerimizi ve fiyatlarımızı inceleyin."),
        new(About, "Hakkımızda", "/hakkimizda", "Hakkımızda", null),
        new(Gallery, "Galeri", "/galeri", "Galeri", "Mekanımızdan ve lezzetlerimizden kareler."),
        new(Contact, "İletişim", "/iletisim", "İletişim", "İletişim bilgilerimiz, adresimiz ve çalışma saatlerimiz."),
        new(Reservation, "Rezervasyon", "/rezervasyon", "Rezervasyon", "Online rezervasyon talebinde bulunun."),
        new(Faq, "Sıkça Sorulan Sorular", "/sss", "Sıkça Sorulan Sorular", "Rezervasyon, menü, sipariş ve çalışma saatleri hakkında sık sorulan sorular.")
    ];

    public static SeoPageDefinition? Find(string key) => All.FirstOrDefault(p => p.Key == key);

    // Serbest metin değil sabit liste - admin geçersiz bir schema.org türü yazamaz.
    public static IReadOnlyList<(string Value, string Label)> LocalBusinessTypes { get; } =
    [
        ("Restaurant", "Restoran"),
        ("CafeOrCoffeeShop", "Kafe / Kahve Dükkanı"),
        ("BarOrPub", "Bar / Pub"),
        ("FastFoodRestaurant", "Fast Food"),
        ("Bakery", "Fırın / Pastane"),
        ("IceCreamShop", "Dondurmacı")
    ];
}
