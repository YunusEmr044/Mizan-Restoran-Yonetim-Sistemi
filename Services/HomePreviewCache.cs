using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RestoranYonetim.Data;
using RestoranYonetim.Services.I18n;
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
        if (cache.TryGetValue(CacheKeys.For(GalleryKey), out List<GalleryImage>? cached) && cached != null)
        {
            return cached;
        }

        await GalleryGate.WaitAsync();
        try
        {
            if (cache.TryGetValue(CacheKeys.For(GalleryKey), out cached) && cached != null)
            {
                return cached;
            }

            var images = await context.GalleryImages
                .AsNoTracking()
                .Where(g => g.ShowOnHomepage)
                .OrderBy(g => g.DisplayOrder)
                .Take(7)
                .ToListAsync();

            await ContentTranslator.ApplyAsync(context, images);
            cache.Set(CacheKeys.For(GalleryKey), images, Ttl);
            return images;
        }
        finally
        {
            GalleryGate.Release();
        }
    }

    public static async Task<List<Campaign>> GetActiveCampaignsAsync(ApplicationDbContext context, IMemoryCache cache)
    {
        if (cache.TryGetValue(CacheKeys.For(CampaignsKey), out List<Campaign>? cached) && cached != null)
        {
            return cached;
        }

        await CampaignsGate.WaitAsync();
        try
        {
            if (cache.TryGetValue(CacheKeys.For(CampaignsKey), out cached) && cached != null)
            {
                return cached;
            }

            var today = DateTime.Today;
            var campaigns = await context.Campaigns
                .AsNoTracking()
                .Where(c => c.IsActive && c.StartDate <= today && c.EndDate >= today)
                .OrderBy(c => c.DisplayOrder)
                .ToListAsync();

            await ContentTranslator.ApplyAsync(context, campaigns);
            cache.Set(CacheKeys.For(CampaignsKey), campaigns, Ttl);
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
        if (cache.TryGetValue(CacheKeys.For(TestimonialsKey), out List<Feedback>? cached) && cached != null)
        {
            return cached;
        }

        await TestimonialsGate.WaitAsync();
        try
        {
            if (cache.TryGetValue(CacheKeys.For(TestimonialsKey), out cached) && cached != null)
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

            cache.Set(CacheKeys.For(TestimonialsKey), testimonials, Ttl);
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
        if (cache.TryGetValue(CacheKeys.For(AllPublishedFeedbackKey), out List<Feedback>? cached) && cached != null)
        {
            return cached;
        }

        await AllPublishedFeedbackGate.WaitAsync();
        try
        {
            if (cache.TryGetValue(CacheKeys.For(AllPublishedFeedbackKey), out cached) && cached != null)
            {
                return cached;
            }

            var feedback = await context.Feedbacks
                .AsNoTracking()
                .Where(f => f.IsPublished)
                .OrderByDescending(f => f.CreatedAt)
                .Take(500)
                .ToListAsync();

            cache.Set(CacheKeys.For(AllPublishedFeedbackKey), feedback, Ttl);
            return feedback;
        }
        finally
        {
            AllPublishedFeedbackGate.Release();
        }
    }

    public static async Task<List<HeroSlide>> GetHeroSlidesAsync(ApplicationDbContext context, IMemoryCache cache)
    {
        if (cache.TryGetValue(CacheKeys.For(HeroKey), out List<HeroSlide>? cached) && cached != null)
        {
            return cached;
        }

        await HeroGate.WaitAsync();
        try
        {
            if (cache.TryGetValue(CacheKeys.For(HeroKey), out cached) && cached != null)
            {
                return cached;
            }

            var slides = await context.HeroSlides
                .AsNoTracking()
                .Where(h => h.IsActive)
                .OrderBy(h => h.DisplayOrder)
                .ToListAsync();

            await ContentTranslator.ApplyAsync(context, slides);
            cache.Set(CacheKeys.For(HeroKey), slides, Ttl);
            return slides;
        }
        finally
        {
            HeroGate.Release();
        }
    }

    public static async Task<List<HomeStat>> GetStatsAsync(ApplicationDbContext context, IMemoryCache cache)
    {
        if (cache.TryGetValue(CacheKeys.For(StatsKey), out List<HomeStat>? cached) && cached != null)
        {
            return cached;
        }

        await StatsGate.WaitAsync();
        try
        {
            if (cache.TryGetValue(CacheKeys.For(StatsKey), out cached) && cached != null)
            {
                return cached;
            }

            var stats = await context.HomeStats
                .AsNoTracking()
                .Where(s => s.IsActive)
                .OrderBy(s => s.DisplayOrder)
                .ToListAsync();

            await ContentTranslator.ApplyAsync(context, stats);
            cache.Set(CacheKeys.For(StatsKey), stats, Ttl);
            return stats;
        }
        finally
        {
            StatsGate.Release();
        }
    }

    public static async Task<List<HomeFeature>> GetFeaturesAsync(ApplicationDbContext context, IMemoryCache cache)
    {
        if (cache.TryGetValue(CacheKeys.For(FeaturesKey), out List<HomeFeature>? cached) && cached != null)
        {
            return cached;
        }

        await FeaturesGate.WaitAsync();
        try
        {
            if (cache.TryGetValue(CacheKeys.For(FeaturesKey), out cached) && cached != null)
            {
                return cached;
            }

            var features = await context.HomeFeatures
                .AsNoTracking()
                .Where(f => f.IsActive)
                .OrderBy(f => f.DisplayOrder)
                .ToListAsync();

            await ContentTranslator.ApplyAsync(context, features);
            cache.Set(CacheKeys.For(FeaturesKey), features, Ttl);
            return features;
        }
        finally
        {
            FeaturesGate.Release();
        }
    }

    public static async Task<List<MenuItem>> GetFeaturedMenuAsync(ApplicationDbContext context, IMemoryCache cache)
    {
        if (cache.TryGetValue(CacheKeys.For(FeaturedMenuKey), out List<MenuItem>? cached) && cached != null)
        {
            return cached;
        }

        await FeaturedMenuGate.WaitAsync();
        try
        {
            if (cache.TryGetValue(CacheKeys.For(FeaturedMenuKey), out cached) && cached != null)
            {
                return cached;
            }

            var items = await context.MenuItems
                .AsNoTracking()
                .Where(m => m.IsFeaturedHome && m.IsAvailable)
                .OrderBy(m => m.FeaturedOrder)
                .Take(4)
                .ToListAsync();

            await ContentTranslator.ApplyAsync(context, items);
            cache.Set(CacheKeys.For(FeaturedMenuKey), items, Ttl);
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
        if (cache.TryGetValue(CacheKeys.For(ChefSpecialKey), out ChefSpecial? cached))
        {
            return cached;
        }

        await ChefSpecialGate.WaitAsync();
        try
        {
            if (cache.TryGetValue(CacheKeys.For(ChefSpecialKey), out cached))
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

            await ContentTranslator.ApplyAsync(context, special, special?.MenuItem);
            cache.Set(CacheKeys.For(ChefSpecialKey), special, Ttl);
            return special;
        }
        finally
        {
            ChefSpecialGate.Release();
        }
    }

    public static async Task<List<HomeSection>> GetEnabledSectionsAsync(ApplicationDbContext context, IMemoryCache cache)
    {
        if (cache.TryGetValue(CacheKeys.For(SectionsKey), out List<HomeSection>? cached) && cached != null)
        {
            return cached;
        }

        await SectionsGate.WaitAsync();
        try
        {
            if (cache.TryGetValue(CacheKeys.For(SectionsKey), out cached) && cached != null)
            {
                return cached;
            }

            var sections = await context.HomeSections
                .AsNoTracking()
                .Where(s => s.IsEnabled)
                .OrderBy(s => s.DisplayOrder)
                .ToListAsync();

            cache.Set(CacheKeys.For(SectionsKey), sections, Ttl);
            return sections;
        }
        finally
        {
            SectionsGate.Release();
        }
    }

    public static async Task<List<WorkingHoursDay>> GetWorkingHoursAsync(ApplicationDbContext context, IMemoryCache cache)
    {
        if (cache.TryGetValue(CacheKeys.For(WorkingHoursKey), out List<WorkingHoursDay>? cached) && cached != null)
        {
            return cached;
        }

        await WorkingHoursGate.WaitAsync();
        try
        {
            if (cache.TryGetValue(CacheKeys.For(WorkingHoursKey), out cached) && cached != null)
            {
                return cached;
            }

            var hours = await context.WorkingHoursDays
                .AsNoTracking()
                .OrderBy(d => d.DayOfWeek)
                .ToListAsync();

            cache.Set(CacheKeys.For(WorkingHoursKey), hours, Ttl);
            return hours;
        }
        finally
        {
            WorkingHoursGate.Release();
        }
    }

    public static void InvalidateGallery(IMemoryCache cache) => CacheKeys.Remove(cache, GalleryKey);
    public static void InvalidateCampaigns(IMemoryCache cache) => CacheKeys.Remove(cache, CampaignsKey);
    public static void InvalidateTestimonials(IMemoryCache cache) => CacheKeys.Remove(cache, TestimonialsKey);
    public static void InvalidateHero(IMemoryCache cache) => CacheKeys.Remove(cache, HeroKey);
    public static void InvalidateStats(IMemoryCache cache) => CacheKeys.Remove(cache, StatsKey);
    public static void InvalidateFeatures(IMemoryCache cache) => CacheKeys.Remove(cache, FeaturesKey);
    public static void InvalidateFeaturedMenu(IMemoryCache cache) => CacheKeys.Remove(cache, FeaturedMenuKey);
    public static void InvalidateChefSpecial(IMemoryCache cache) => CacheKeys.Remove(cache, ChefSpecialKey);
    public static void InvalidateSections(IMemoryCache cache) => CacheKeys.Remove(cache, SectionsKey);
    public static void InvalidateWorkingHours(IMemoryCache cache) => CacheKeys.Remove(cache, WorkingHoursKey);
}
