namespace RestoranYonetim.Data;

// Geliştirme ortamı demo görselleri; kaynakları docs/GORSEL-KAYNAKLARI.md içinde.
public static class DemoImages
{
    public static readonly IReadOnlyDictionary<string, string> MenuItems = new Dictionary<string, string>
    {
        ["Adana Kebap"] = "/demo/menu/adana-kebap.webp",
        ["Karışık Izgara"] = "/demo/menu/karisik-izgara.webp",
        ["Et Döner"] = "/demo/menu/et-doner.webp",
        ["Tavuk Döner Dürüm"] = "/demo/menu/tavuk-doner-durum.webp",
        ["Lahmacun"] = "/demo/menu/lahmacun.webp",
        ["Kıymalı Pide"] = "/demo/menu/kiymali-pide.webp",
        ["Karışık Pide"] = "/demo/menu/karisik-pide.webp",
        ["Serpme Kahvaltı"] = "/demo/menu/serpme-kahvalti.webp",
        ["Sucuklu Yumurta"] = "/demo/menu/sucuklu-yumurta.webp",
        ["Mercimek Çorbası"] = "/demo/menu/mercimek-corbasi.webp",
        ["Humus"] = "/demo/menu/humus.webp",
        ["Karışık Meze Tabağı"] = "/demo/menu/karisik-meze-tabagi.webp",
        ["Fıstıklı Baklava"] = "/demo/menu/fistikli-baklava.webp",
        ["Türk Çayı"] = "/demo/menu/turk-cayi.webp",
        ["Türk Kahvesi"] = "/demo/menu/turk-kahvesi.webp"
    };

    public static readonly IReadOnlyList<string> FeaturedMenuItems =
        ["Adana Kebap", "Karışık Izgara", "Lahmacun", "Serpme Kahvaltı", "Karışık Meze Tabağı", "Fıstıklı Baklava"];

    public static readonly IReadOnlyList<(string Url, string Caption, bool Home)> Gallery =
    [
        ("/demo/galeri/ates.webp", "Ocakbaşında meşe kömürü", true),
        ("/demo/galeri/meze.webp", "Günün mezeleri", true),
        ("/demo/galeri/salon.webp", "Salonumuz", true),
        ("/demo/galeri/lahmacun.webp", "Taş fırından lahmacun", true),
        ("/demo/galeri/bahce.webp", "Bahçe masalarımız", false),
        ("/demo/galeri/sofra.webp", "Kalabalık sofralar", false),
        ("/demo/galeri/sef.webp", "Mutfaktan servise", false),
        ("/demo/galeri/cay.webp", "Yemek sonrası demli çay", false)
    ];

    public static readonly IReadOnlyList<(string Url, string Title, string Subtitle)> HeroSlides =
    [
        ("/demo/hero/ocakbasi.webp", "Közün Başında, Ustanın Elinden", "Meşe kömüründe pişen kebaplar, her gün taze çekilen kıyma."),
        ("/demo/hero/sofra.webp", "Mezeler, Çay ve Uzun Sohbetler", "Annelerimizin tariflerinden ev yemekleri ve günlük mezeler."),
        ("/demo/hero/salon.webp", "Sıcak Bir Sofra, Güler Yüzlü Servis", "Salonda ya da bahçede; rezervasyonla yeriniz hazır.")
    ];

    public const string About = "/demo/site/hakkimizda.webp";
    public const string ReservationCta = "/demo/site/rezervasyon.webp";
    public const string QrPhone = "/demo/site/qr.webp";
}
