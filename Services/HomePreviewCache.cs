using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RestoranYonetim.Data;
using RestoranYonetim.Models;

namespace RestoranYonetim.Services;

// Anasayfa bölümleri için kısa süreli önbellek. ÖNEMLİ: "cache stampede" riskine karşı kısa TTL +
// tek-uçuşlu (single-flight) kilit deseni kullanılır; her bölüm ayrı anahtar/kilit kullanır ki
// bir bölümdeki değişiklik diğerlerinin önbelleğini gereksiz geçersiz kılmasın.
public static class HomePreviewCache
{
    private const string GalleryKey = "home-gallery-preview";
    private const string CampaignsKey = "home-active-campaigns";
    private const string TestimonialsKey = "home-testimonials";
    private const string AllPublishedFeedbackKey = "home-all-published-feedback";
    private const string HeroKey = "home-hero-slides";
    private const string StatsKey = "home-stats";
    private const string FeaturesKey = "home-features";
    private const string FeaturedMenuKey = "home-featured-menu";
    private const string ChefSpecialKey = "home-chef-special";
    private const string SectionsKey = "home-sections";
    private const string WorkingHoursKey = "home-working-hours";

    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(60);

    private static readonly SemaphoreSlim GalleryGate = new(1, 1);
    private static readonly SemaphoreSlim CampaignsGate = new(1, 1);
    private static readonly SemaphoreSlim TestimonialsGate = new(1, 1);
    private static readonly SemaphoreSlim AllPublishedFeedbackGate = new(1, 1);
    private static readonly SemaphoreSlim HeroGate = new(1, 1);
    private static readonly SemaphoreSlim StatsGate = new(1, 1);
    private static readonly SemaphoreSlim FeaturesGate = new(1, 1);
    private static readonly SemaphoreSlim FeaturedMenuGate = new(1, 1);
    private static readonly SemaphoreSlim ChefSpecialGate = new(1, 1);
    private static readonly SemaphoreSlim SectionsGate = new(1, 1);
    private static readonly SemaphoreSlim WorkingHoursGate = new(1, 1);

    public static async Task<List<GalleryImage>> GetGalleryPreviewAsync(ApplicationDbContext context, IMemoryCache cache)
    {
        if (cache.TryGetValue(GalleryKey, out List<GalleryImage>? cached) && cached != null)
        {
            return cached;
        }

        await GalleryGate.WaitAsync();
        try
        {
            if (cache.TryGetValue(GalleryKey, out cached) && cached != null)
            {
                return cached;
            }

            var images = await context.GalleryImages
                .AsNoTracking()
                .Where(g => g.ShowOnHomepage)
                .OrderBy(g => g.DisplayOrder)
                .Take(7)
                .ToListAsync();

            cache.Set(GalleryKey, images, Ttl);
            return images;
        }
        finally
        {
            GalleryGate.Release();
        }
    }

    public static async Task<List<Campaign>> GetActiveCampaignsAsync(ApplicationDbContext context, IMemoryCache cache)
    {
        if (cache.TryGetValue(CampaignsKey, out List<Campaign>? cached) && cached != null)
        {
            return cached;
        }

        await CampaignsGate.WaitAsync();
        try
        {
            if (cache.TryGetValue(CampaignsKey, out cached) && cached != null)
            {
                return cached;
            }

            var today = DateTime.Today;
            var campaigns = await context.Campaigns
                .AsNoTracking()
                .Where(c => c.IsActive && c.StartDate <= today && c.EndDate >= today)
                .OrderBy(c => c.DisplayOrder)
                .ToListAsync();

            cache.Set(CampaignsKey, campaigns, Ttl);
            return campaigns;
        }
        finally
        {
            CampaignsGate.Release();
        }
    }

    // Admin yorumları elle seçip sıralamışsa (HomeDisplayOrder dolu) önce onlar kullanılır;
    // hiç seçim yoksa en az 4 yıldızlı en yeni 6 yoruma düşülür (sistem boş görünmesin diye).
    public static async Task<List<Feedback>> GetTestimonialsAsync(ApplicationDbContext context, IMemoryCache cache)
    {
        if (cache.TryGetValue(TestimonialsKey, out List<Feedback>? cached) && cached != null)
        {
            return cached;
        }

        await TestimonialsGate.WaitAsync();
        try
        {
            if (cache.TryGetValue(TestimonialsKey, out cached) && cached != null)
            {
                return cached;
            }

            var selected = await context.Feedbacks
                .AsNoTracking()
                .Where(f => f.IsPublished && f.HomeDisplayOrder != null)
                .OrderBy(f => f.HomeDisplayOrder)
                .Take(6)
                .ToListAsync();

            var testimonials = selected.Count > 0
                ? selected
                : await context.Feedbacks
                    .AsNoTracking()
                    .Where(f => f.IsPublished && f.Rating >= 4 && !string.IsNullOrEmpty(f.Comment))
                    .OrderByDescending(f => f.CreatedAt)
                    .Take(6)
                    .ToListAsync();

            cache.Set(TestimonialsKey, testimonials, Ttl);
            return testimonials;
        }
        finally
        {
            TestimonialsGate.Release();
        }
    }

    // ÖNEMLİ: AggregateRating için TÜM yayınlanmış yorumlar gerekir, GetTestimonialsAsync'teki
    // seçilmiş 6'lık alt küme kullanılırsa sahte yüksek bir ortalama üretilir.
    public static async Task<List<Feedback>> GetAllPublishedFeedbackAsync(ApplicationDbContext context, IMemoryCache cache)
    {
        if (cache.TryGetValue(AllPublishedFeedbackKey, out List<Feedback>? cached) && cached != null)
        {
            return cached;
        }

        await AllPublishedFeedbackGate.WaitAsync();
        try
        {
            if (cache.TryGetValue(AllPublishedFeedbackKey, out cached) && cached != null)
            {
                return cached;
            }

            var feedback = await context.Feedbacks
                .AsNoTracking()
                .Where(f => f.IsPublished)
                .OrderByDescending(f => f.CreatedAt)
                .Take(500)
                .ToListAsync();

            cache.Set(AllPublishedFeedbackKey, feedback, Ttl);
            return feedback;
        }
        finally
        {
            AllPublishedFeedbackGate.Release();
        }
    }

    public static async Task<List<HeroSlide>> GetHeroSlidesAsync(ApplicationDbContext context, IMemoryCache cache)
    {
        if (cache.TryGetValue(HeroKey, out List<HeroSlide>? cached) && cached != null)
        {
            return cached;
        }

        await HeroGate.WaitAsync();
        try
        {
            if (cache.TryGetValue(HeroKey, out cached) && cached != null)
            {
                return cached;
            }

            var slides = await context.HeroSlides
                .AsNoTracking()
                .Where(h => h.IsActive)
                .OrderBy(h => h.DisplayOrder)
                .ToListAsync();

            cache.Set(HeroKey, slides, Ttl);
            return slides;
        }
        finally
        {
            HeroGate.Release();
        }
    }

    public static async Task<List<HomeStat>> GetStatsAsync(ApplicationDbContext context, IMemoryCache cache)
    {
        if (cache.TryGetValue(StatsKey, out List<HomeStat>? cached) && cached != null)
        {
            return cached;
        }

        await StatsGate.WaitAsync();
        try
        {
            if (cache.TryGetValue(StatsKey, out cached) && cached != null)
            {
                return cached;
            }

            var stats = await context.HomeStats
                .AsNoTracking()
                .Where(s => s.IsActive)
                .OrderBy(s => s.DisplayOrder)
                .ToListAsync();

            cache.Set(StatsKey, stats, Ttl);
            return stats;
        }
        finally
        {
            StatsGate.Release();
        }
    }

    public static async Task<List<HomeFeature>> GetFeaturesAsync(ApplicationDbContext context, IMemoryCache cache)
    {
        if (cache.TryGetValue(FeaturesKey, out List<HomeFeature>? cached) && cached != null)
        {
            return cached;
        }

        await FeaturesGate.WaitAsync();
        try
        {
            if (cache.TryGetValue(FeaturesKey, out cached) && cached != null)
            {
                return cached;
            }

            var features = await context.HomeFeatures
                .AsNoTracking()
                .Where(f => f.IsActive)
                .OrderBy(f => f.DisplayOrder)
                .ToListAsync();

            cache.Set(FeaturesKey, features, Ttl);
            return features;
        }
        finally
        {
            FeaturesGate.Release();
        }
    }

    public static async Task<List<MenuItem>> GetFeaturedMenuAsync(ApplicationDbContext context, IMemoryCache cache)
    {
        if (cache.TryGetValue(FeaturedMenuKey, out List<MenuItem>? cached) && cached != null)
        {
            return cached;
        }

        await FeaturedMenuGate.WaitAsync();
        try
        {
            if (cache.TryGetValue(FeaturedMenuKey, out cached) && cached != null)
            {
                return cached;
            }

            var items = await context.MenuItems
                .AsNoTracking()
                .Where(m => m.IsFeaturedHome && m.IsAvailable)
                .OrderBy(m => m.FeaturedOrder)
                .Take(4)
                .ToListAsync();

            cache.Set(FeaturedMenuKey, items, Ttl);
            return items;
        }
        finally
        {
            FeaturedMenuGate.Release();
        }
    }

    // Tarih aralığı içinde aktif tek kayıt döner (çakışırsa en son başlayan); süresi dolan öneri asla dönmez.
    public static async Task<ChefSpecial?> GetChefSpecialAsync(ApplicationDbContext context, IMemoryCache cache)
    {
        if (cache.TryGetValue(ChefSpecialKey, out ChefSpecial? cached))
        {
            return cached;
        }

        await ChefSpecialGate.WaitAsync();
        try
        {
            if (cache.TryGetValue(ChefSpecialKey, out cached))
            {
                return cached;
            }

            var today = DateTime.Today;
            var special = await context.ChefSpecials
                .AsNoTracking()
                .Include(c => c.MenuItem)
                .Where(c => c.IsActive && c.StartDate <= today && c.EndDate >= today)
                .OrderByDescending(c => c.StartDate)
                .FirstOrDefaultAsync();

            cache.Set(ChefSpecialKey, special, Ttl);
            return special;
        }
        finally
        {
            ChefSpecialGate.Release();
        }
    }

    public static async Task<List<HomeSection>> GetEnabledSectionsAsync(ApplicationDbContext context, IMemoryCache cache)
    {
        if (cache.TryGetValue(SectionsKey, out List<HomeSection>? cached) && cached != null)
        {
            return cached;
        }

        await SectionsGate.WaitAsync();
        try
        {
            if (cache.TryGetValue(SectionsKey, out cached) && cached != null)
            {
                return cached;
            }

            var sections = await context.HomeSections
                .AsNoTracking()
                .Where(s => s.IsEnabled)
                .OrderBy(s => s.DisplayOrder)
                .ToListAsync();

            cache.Set(SectionsKey, sections, Ttl);
            return sections;
        }
        finally
        {
            SectionsGate.Release();
        }
    }

    public static async Task<List<WorkingHoursDay>> GetWorkingHoursAsync(ApplicationDbContext context, IMemoryCache cache)
    {
        if (cache.TryGetValue(WorkingHoursKey, out List<WorkingHoursDay>? cached) && cached != null)
        {
            return cached;
        }

        await WorkingHoursGate.WaitAsync();
        try
        {
            if (cache.TryGetValue(WorkingHoursKey, out cached) && cached != null)
            {
                return cached;
            }

            var hours = await context.WorkingHoursDays
                .AsNoTracking()
                .OrderBy(d => d.DayOfWeek)
                .ToListAsync();

            cache.Set(WorkingHoursKey, hours, Ttl);
            return hours;
        }
        finally
        {
            WorkingHoursGate.Release();
        }
    }

    public static void InvalidateGallery(IMemoryCache cache) => cache.Remove(GalleryKey);
    public static void InvalidateCampaigns(IMemoryCache cache) => cache.Remove(CampaignsKey);
    public static void InvalidateTestimonials(IMemoryCache cache) => cache.Remove(TestimonialsKey);
    public static void InvalidateHero(IMemoryCache cache) => cache.Remove(HeroKey);
    public static void InvalidateStats(IMemoryCache cache) => cache.Remove(StatsKey);
    public static void InvalidateFeatures(IMemoryCache cache) => cache.Remove(FeaturesKey);
    public static void InvalidateFeaturedMenu(IMemoryCache cache) => cache.Remove(FeaturedMenuKey);
    public static void InvalidateChefSpecial(IMemoryCache cache) => cache.Remove(ChefSpecialKey);
    public static void InvalidateSections(IMemoryCache cache) => cache.Remove(SectionsKey);
    public static void InvalidateWorkingHours(IMemoryCache cache) => cache.Remove(WorkingHoursKey);
}
