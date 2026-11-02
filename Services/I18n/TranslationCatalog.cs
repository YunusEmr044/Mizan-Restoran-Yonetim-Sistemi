namespace RestoranYonetim.Services.I18n;

public sealed record TranslationTab(string Key, string Title, string Icon, IReadOnlyList<string> EntityTypes);

// Panel "Çeviriler" sayfasının sekmeleri; her çevrilebilir kayıt türü tek bir sekmede yer alır.
public static class TranslationCatalog
{
    public const string TargetLanguage = "en";

    private static readonly HashSet<string> LongFields = new(StringComparer.Ordinal)
    {
        "Description", "Answer", "Body", "AboutText", "Subtitle", "MetaDescription", "OgDescription", "SeoDescription",
        "FooterText", "FooterTagline", "QrSectionSubtitle", "QrStep1Text", "QrStep2Text", "QrStep3Text", "ReservationCtaText",
        "HeroSubtitle", "DescriptionOverride", "WorkingHours", "WhatsAppDefaultMessage"
    };

    public static IReadOnlyList<TranslationTab> Tabs { get; } = new[]
    {
        new TranslationTab("menu", "Menü", "bi-journal-richtext", new[] { "Category", "MenuItem", "MenuItemOption" }),
        new TranslationTab("site", "Site metinleri", "bi-window", new[] { "SiteContent" }),
        new TranslationTab("home", "Ana sayfa", "bi-layout-text-window", new[] { "HeroSlide", "HomeStat", "HomeFeature", "ChefSpecial" }),
        new TranslationTab("faq", "SSS", "bi-question-circle", new[] { "Faq" }),
        new TranslationTab("campaigns", "Kampanyalar", "bi-megaphone", new[] { "Campaign" }),
        new TranslationTab("gallery", "Galeri", "bi-images", new[] { "GalleryImage" }),
        new TranslationTab("legal", "Yasal sayfalar", "bi-file-earmark-text", new[] { "LegalPage" }),
        new TranslationTab("seo", "SEO", "bi-graph-up-arrow", new[] { "PageSeo" }),
    };

    public static TranslationTab? Find(string? key) => Tabs.FirstOrDefault(t => t.Key == key);

    public static bool IsAllowed(string tab, string entityType, string field) =>
        Find(tab)?.EntityTypes.Contains(entityType) == true && TranslationOverlay.IsTranslatable(entityType, field);

    public static bool IsLong(string field, string turkish) => LongFields.Contains(field) || turkish.Length > 80;
}
