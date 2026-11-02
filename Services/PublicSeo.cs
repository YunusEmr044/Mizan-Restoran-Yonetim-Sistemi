using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using RestoranYonetim.Data;
using RestoranYonetim.Models;
using RestoranYonetim.Security;
using RestoranYonetim.Services.I18n;
using RestoranYonetim.Services.Publishing;

namespace RestoranYonetim.Services;

public static class PublicSeo
{
    public const string DefaultOgImage = "/img/og-default.png";

    // Bazı paylaşım önizlemeleri webp göstermediği için webp adaylar atlanır.
    public static string PickOgImage(params string?[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate)) continue;
            var path = candidate.Split('?', '#')[0];
            if (!path.EndsWith(".webp", StringComparison.OrdinalIgnoreCase)) return candidate;
        }

        return DefaultOgImage;
    }

    // SEO alanlarını PageSeo → SeoSettings → kod içi varsayılan sırasıyla çözüp ViewBag'e yazar.
    public static async Task ApplyAsync(Controller controller, ApplicationDbContext context, IMemoryCache cache, IConfiguration configuration,
        SiteContent content, string pageKey, SeoPageDefinition? page = null)
    {
        var viewBag = controller.ViewBag;
        var seoSettings = await SeoCache.GetSettingsAsync(context, cache);
        var pageSeo = await SeoCache.GetPageAsync(context, cache, pageKey);
        var pageDef = page ?? SeoPageCatalog.Find(pageKey);
        var baseUrl = await PublicUrl.ResolveAsync(controller.HttpContext, context, cache, configuration);
        var isHome = pageKey == SeoPageCatalog.Home;
        var t = controller.HttpContext.RequestServices.GetRequiredService<TextLocalizer>();

        var (fullTitle, resolvedDescription) = SeoResolver.Resolve(new SeoInputs(
            IsHome: isHome,
            PageTitle: pageSeo?.Title,
            SiteSeoTitle: content.SeoTitle,
            CatalogTitle: pageDef == null ? null : t[pageDef.DefaultTitle],
            PageDescription: pageSeo?.MetaDescription,
            SiteSeoDescription: content.SeoDescription,
            CatalogDescription: pageDef?.DefaultDescription == null ? null : t[pageDef.DefaultDescription],
            DefaultDescription: seoSettings.DefaultMetaDescription,
            HeroTitle: content.HeroTitle,
            HeroSubtitle: content.HeroSubtitle));

        // Kanonik adres sayfanın kendi dilindeki adresidir; diğer diller hreflang ile bildirilir (x-default Türkçe).
        var pagePath = pageDef?.Path ?? "/";
        var root = baseUrl.TrimEnd('/');
        var canonical = !string.IsNullOrWhiteSpace(pageSeo?.CanonicalUrl)
            ? StructuredDataService.AbsoluteUrl(baseUrl, pageSeo!.CanonicalUrl)
            : root + SiteLanguages.PathFor(SiteLanguages.Current, pagePath, QueryString.Empty);
        viewBag.SeoAlternates = SiteLanguages.All.Select(l => (Lang: l, Url: root + SiteLanguages.PathFor(l, pagePath, QueryString.Empty)))
            .Append((Lang: "x-default", Url: root + pagePath))
            .ToList();

        var ogImage = PickOgImage(pageSeo?.OgImageUrl, seoSettings.DefaultOgImageUrl, content.HeroImageUrl, content.LogoImageUrl);

        viewBag.SeoPageTitle = fullTitle;
        viewBag.SeoMetaDescription = resolvedDescription;
        viewBag.SeoCanonicalUrl = canonical;
        viewBag.SeoNoIndex = !seoSettings.IndexingEnabled || (pageSeo?.NoIndex ?? false);
        viewBag.SeoNoFollow = pageSeo?.NoFollow ?? false;
        viewBag.SeoOgTitle = pageSeo?.OgTitle ?? fullTitle;
        viewBag.SeoOgDescription = pageSeo?.OgDescription ?? resolvedDescription;
        viewBag.SeoOgImageUrl = StructuredDataService.AbsoluteUrl(baseUrl, ogImage);
        viewBag.SeoGoogleVerification = seoSettings.GoogleSiteVerification;
        viewBag.SeoBingVerification = seoSettings.BingSiteVerification;
        viewBag.SeoTwitterHandle = seoSettings.TwitterHandle;
        viewBag.SeoBaseUrl = baseUrl;
        viewBag.SeoRestaurantId = StructuredDataService.RestaurantId(baseUrl);
    }
}
