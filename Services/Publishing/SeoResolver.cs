namespace RestoranYonetim.Services.Publishing;

public sealed record SeoInputs(
    bool IsHome,
    string? PageTitle,
    string? SiteSeoTitle,
    string? CatalogTitle,
    string? PageDescription,
    string? SiteSeoDescription,
    string? CatalogDescription,
    string? DefaultDescription,
    string? HeroTitle,
    string? HeroSubtitle);

// Sayfa başlığı ve meta açıklaması: PageSeo → site SEO alanı (yalnızca ana sayfa) → katalog varsayılanı → hero metinleri.
public static class SeoResolver
{
    public static (string Title, string? Description) Resolve(SeoInputs i)
    {
        var heroTitle = Clean(i.HeroTitle) ?? string.Empty;

        string title;
        if (i.IsHome)
        {
            var subtitle = Clean(i.HeroSubtitle);
            title = Clean(i.PageTitle) ?? Clean(i.SiteSeoTitle)
                ?? (subtitle == null ? heroTitle : $"{heroTitle} — {subtitle}");
        }
        else
        {
            var pageTitle = Clean(i.PageTitle) ?? Clean(i.CatalogTitle);
            title = pageTitle == null ? heroTitle : $"{pageTitle} | {heroTitle}";
        }

        var description = Clean(i.PageDescription)
            ?? (i.IsHome ? Clean(i.SiteSeoDescription) : null)
            ?? Clean(i.CatalogDescription)
            ?? Clean(i.DefaultDescription)
            ?? Clean(i.HeroSubtitle);

        return (title, description);
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
