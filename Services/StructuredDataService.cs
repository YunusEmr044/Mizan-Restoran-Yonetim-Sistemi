using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using RestoranYonetim.Models;

namespace RestoranYonetim.Services;

// Schema.org yapılandırılmış veri (JSON-LD) üretimi. Boş alan varsa ilgili özellik hiç üretilmez, sahte/varsayımsal veri eklenmez.
// Saf bir builder: veritabanına gitmez, veriyi çağıran taraf önbellekten verir.
public static class StructuredDataService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly Dictionary<DayOfWeek, string> SchemaDayUris = new()
    {
        [DayOfWeek.Monday] = "https://schema.org/Monday",
        [DayOfWeek.Tuesday] = "https://schema.org/Tuesday",
        [DayOfWeek.Wednesday] = "https://schema.org/Wednesday",
        [DayOfWeek.Thursday] = "https://schema.org/Thursday",
        [DayOfWeek.Friday] = "https://schema.org/Friday",
        [DayOfWeek.Saturday] = "https://schema.org/Saturday",
        [DayOfWeek.Sunday] = "https://schema.org/Sunday"
    };

    public static string RestaurantId(string baseUrl) => $"{baseUrl.TrimEnd('/')}/#restoran";

    public static string AbsoluteUrl(string baseUrl, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return baseUrl.TrimEnd('/') + "/";
        }
        if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || path.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return path;
        }
        return baseUrl.TrimEnd('/') + "/" + path.TrimStart('/');
    }

    // 0 kayıt varken (0,0) döner; sahte puan üretilmesin diye çağıran taraf bu durumda aggregateRating eklemez.
    public static (double Average, int Count) ComputeRating(List<Feedback> published) =>
        published.Count == 0 ? (0, 0) : (published.Average(f => f.Rating), published.Count);

    public static Dictionary<string, object?> BuildRestaurant(SiteContent site, SeoSettings seo, List<WorkingHoursDay> hours, (double Average, int Count) rating, string baseUrl)
    {
        var node = new Dictionary<string, object?>
        {
            ["@type"] = string.IsNullOrWhiteSpace(seo.LocalBusinessType) ? "Restaurant" : seo.LocalBusinessType,
            ["@id"] = RestaurantId(baseUrl),
            ["name"] = site.HeroTitle,
            ["url"] = baseUrl.TrimEnd('/') + "/"
        };

        if (!string.IsNullOrWhiteSpace(site.LogoImageUrl)) node["logo"] = AbsoluteUrl(baseUrl, site.LogoImageUrl);
        if (!string.IsNullOrWhiteSpace(site.HeroImageUrl)) node["image"] = AbsoluteUrl(baseUrl, site.HeroImageUrl);
        if (!string.IsNullOrWhiteSpace(site.ContactPhone)) node["telephone"] = site.ContactPhone;
        if (!string.IsNullOrWhiteSpace(site.ContactEmail)) node["email"] = site.ContactEmail;
        if (!string.IsNullOrWhiteSpace(seo.PriceRange)) node["priceRange"] = seo.PriceRange;

        if (!string.IsNullOrWhiteSpace(site.ContactAddress))
        {
            node["address"] = new Dictionary<string, object?>
            {
                ["@type"] = "PostalAddress",
                ["streetAddress"] = site.ContactAddress
            };
        }

        var sameAs = new List<string>();
        if (!string.IsNullOrWhiteSpace(site.FacebookUrl)) sameAs.Add(site.FacebookUrl!);
        if (!string.IsNullOrWhiteSpace(site.InstagramUrl)) sameAs.Add(site.InstagramUrl!);
        if (sameAs.Count > 0) node["sameAs"] = sameAs;

        // Gece yarısını geçen saatler (ör. 18:00-02:00) olduğu gibi yazılır; Schema.org bu kullanımı destekler.
        var openingHours = hours.Where(d => !d.IsClosed)
            .Select(d => (object)new Dictionary<string, object?>
            {
                ["@type"] = "OpeningHoursSpecification",
                ["dayOfWeek"] = SchemaDayUris[d.DayOfWeek],
                ["opens"] = d.OpenTime.ToString(@"hh\:mm"),
                ["closes"] = d.CloseTime.ToString(@"hh\:mm")
            })
            .Cast<object>()
            .ToList();
        if (openingHours.Count > 0) node["openingHoursSpecification"] = openingHours;

        if (rating.Count > 0)
        {
            node["aggregateRating"] = new Dictionary<string, object?>
            {
                ["@type"] = "AggregateRating",
                ["ratingValue"] = Math.Round(rating.Average, 1),
                ["reviewCount"] = rating.Count,
                ["bestRating"] = 5,
                ["worstRating"] = 1
            };
        }

        return node;
    }

    // Sahte/uydurma yorum üretilmez, sadece admin onaylı yayınlanmış yorumlar kullanılır.
    public static List<object> BuildReviews(List<Feedback> published, string restaurantId, int take = 10)
    {
        return published
            .Where(f => !string.IsNullOrWhiteSpace(f.Comment))
            .OrderByDescending(f => f.CreatedAt)
            .Take(take)
            .Select(f => (object)new Dictionary<string, object?>
            {
                ["@type"] = "Review",
                ["itemReviewed"] = new Dictionary<string, object?> { ["@id"] = restaurantId },
                ["author"] = new Dictionary<string, object?> { ["@type"] = "Person", ["name"] = string.IsNullOrWhiteSpace(f.Name) ? "Misafir" : f.Name },
                ["reviewRating"] = new Dictionary<string, object?> { ["@type"] = "Rating", ["ratingValue"] = f.Rating, ["bestRating"] = 5, ["worstRating"] = 1 },
                ["reviewBody"] = f.Comment,
                ["datePublished"] = f.CreatedAt.ToString("yyyy-MM-dd")
            })
            .ToList();
    }

    // SADECE menüde görünen (IsAvailable) ürünlerden üretilir; ikinci bir elle senkronize kopya yok.
    public static Dictionary<string, object?> BuildMenu(List<Category> categories, string baseUrl, string restaurantId)
    {
        var sections = new List<object>();
        foreach (var cat in categories.OrderBy(c => c.DisplayOrder))
        {
            var items = cat.MenuItems.Where(m => m.IsAvailable).ToList();
            if (items.Count == 0) continue;

            var menuItems = items.Select(m =>
            {
                var itemNode = new Dictionary<string, object?>
                {
                    ["@type"] = "MenuItem",
                    ["name"] = m.Name
                };
                if (!string.IsNullOrWhiteSpace(m.Description)) itemNode["description"] = m.Description;
                if (!string.IsNullOrWhiteSpace(m.ImageUrl)) itemNode["image"] = AbsoluteUrl(baseUrl, m.ImageUrl);
                itemNode["offers"] = new Dictionary<string, object?>
                {
                    ["@type"] = "Offer",
                    ["price"] = m.Price.ToString("F2", CultureInfo.InvariantCulture),
                    ["priceCurrency"] = "TRY",
                    ["availability"] = "https://schema.org/InStock"
                };
                return (object)itemNode;
            }).ToList();

            sections.Add(new Dictionary<string, object?>
            {
                ["@type"] = "MenuSection",
                ["name"] = cat.Name,
                ["hasMenuItem"] = menuItems
            });
        }

        return new Dictionary<string, object?>
        {
            ["@type"] = "Menu",
            ["@id"] = baseUrl.TrimEnd('/') + "/menu#menu",
            ["name"] = "Menü",
            ["hasMenuSection"] = sections,
            ["provider"] = new Dictionary<string, object?> { ["@id"] = restaurantId }
        };
    }

    public static Dictionary<string, object?> BuildFaqPage(List<Faq> activeFaqs)
    {
        var entities = activeFaqs.Select(f => (object)new Dictionary<string, object?>
        {
            ["@type"] = "Question",
            ["name"] = f.Question,
            ["acceptedAnswer"] = new Dictionary<string, object?>
            {
                ["@type"] = "Answer",
                ["text"] = f.Answer
            }
        }).ToList();

        return new Dictionary<string, object?>
        {
            ["@type"] = "FAQPage",
            ["mainEntity"] = entities
        };
    }

    public static Dictionary<string, object?> BuildBreadcrumbList(IReadOnlyList<(string Name, string Url)> items)
    {
        var listItems = items.Select((item, index) => (object)new Dictionary<string, object?>
        {
            ["@type"] = "ListItem",
            ["position"] = index + 1,
            ["name"] = item.Name,
            ["item"] = item.Url
        }).ToList();

        return new Dictionary<string, object?>
        {
            ["@type"] = "BreadcrumbList",
            ["itemListElement"] = listItems
        };
    }

    // "</" -> "<\/" kaçışı ÖNEMLİ: kullanıcı metninde "</script>" varsa tarayıcı script bloğunu erken kapatmasın diye.
    public static string ToScriptTag(IEnumerable<object> nodes)
    {
        var graph = new Dictionary<string, object?>
        {
            ["@context"] = "https://schema.org",
            ["@graph"] = nodes.ToList()
        };
        var json = JsonSerializer.Serialize(graph, JsonOptions);
        json = json.Replace("</", "<\\/");
        return $"<script type=\"application/ld+json\">{json}</script>";
    }
}
