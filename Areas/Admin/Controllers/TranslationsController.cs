using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RestoranYonetim.Data;
using RestoranYonetim.Models;
using RestoranYonetim.Security;
using RestoranYonetim.Services;
using RestoranYonetim.Services.I18n;

namespace RestoranYonetim.Areas.Admin.Controllers;

public sealed record TranslationInput(string EntityType, int EntityId, string Field, string? Value);

public sealed record TranslationRow(string EntityType, int EntityId, string RecordLabel, string Field, string FieldLabel, string Turkish, string? English, bool IsLong, int? MaxLength);

public sealed record TranslationsView(TranslationTab Tab, IReadOnlyList<TranslationRow> Rows, IReadOnlyDictionary<string, int> MissingByTab, bool MissingOnly,
    string? Query = null, int Page = 1, int TotalPages = 1, int TotalRows = 0);

// Müşteri tarafı içeriğinin İngilizcesi: solda Türkçe metin, sağda çeviri; boş bırakılan çeviri sitede Türkçe görünür.
[Area("Admin")]
[Authorize(Policy = Permissions.SiteContent.Manage)]
public class TranslationsController : Controller
{
    private const int MaxItemsPerSave = 3000;

    private readonly ApplicationDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly AuditService _audit;

    public TranslationsController(ApplicationDbContext context, IMemoryCache cache, AuditService audit)
    {
        _context = context;
        _cache = cache;
        _audit = audit;
    }

    public const int PageSize = 60;

    public async Task<IActionResult> Index(string tab = "menu", bool missingOnly = false, string? q = null, int page = 1)
    {
        var current = TranslationCatalog.Find(tab) ?? TranslationCatalog.Tabs[0];
        var missing = new Dictionary<string, int>();
        IReadOnlyList<TranslationRow> rows = Array.Empty<TranslationRow>();
        foreach (var t in TranslationCatalog.Tabs)
        {
            var tabRows = await LoadRowsAsync(t);
            missing[t.Key] = tabRows.Count(r => string.IsNullOrWhiteSpace(r.English));
            if (t == current)
            {
                rows = missingOnly ? tabRows.Where(r => string.IsNullOrWhiteSpace(r.English)).ToList() : tabRows;
            }
        }
        var query = q?.Trim();
        if (!string.IsNullOrEmpty(query))
        {
            var tr = new System.Globalization.CultureInfo("tr-TR").CompareInfo;
            bool Has(string? text) => text != null && tr.IndexOf(text, query, System.Globalization.CompareOptions.IgnoreCase) >= 0;
            rows = rows.Where(r => Has(r.RecordLabel) || Has(r.Turkish) || Has(r.English)).ToList();
        }
        var totalPages = Math.Max(1, (int)Math.Ceiling(rows.Count / (double)PageSize));
        page = Math.Clamp(page, 1, totalPages);
        return View(new TranslationsView(current, rows.Skip((page - 1) * PageSize).Take(PageSize).ToList(), missing, missingOnly, query, page, totalPages, rows.Count));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestFormLimits(ValueCountLimit = 4 * MaxItemsPerSave + 16)]
    public async Task<IActionResult> Save(string tab, List<TranslationInput>? items, bool missingOnly = false, string? q = null, int page = 1)
    {
        var current = TranslationCatalog.Find(tab);
        items ??= new();
        if (current == null || items.Count > MaxItemsPerSave || items.Any(i => !TranslationCatalog.IsAllowed(current.Key, i.EntityType, i.Field)))
        {
            return BadRequest();
        }

        var lang = TranslationCatalog.TargetLanguage;
        var types = current.EntityTypes.ToList();
        var existing = await _context.ContentTranslations
            .Where(t => types.Contains(t.EntityType) && t.Language == lang)
            .ToDictionaryAsync(t => (t.EntityType, t.EntityId, t.Field));
        var known = (await LoadRowsAsync(current)).Select(r => (r.EntityType, r.EntityId, r.Field)).ToHashSet();

        var changed = 0;
        foreach (var item in items.Where(i => known.Contains((i.EntityType, i.EntityId, i.Field))))
        {
            var value = item.Value?.Trim() ?? string.Empty;
            var maxLength = MaxLength(item.EntityType, item.Field);
            if (maxLength is int max && value.Length > max)
            {
                value = value[..max];
            }
            existing.TryGetValue((item.EntityType, item.EntityId, item.Field), out var row);
            if (value.Length == 0)
            {
                if (row != null)
                {
                    _context.ContentTranslations.Remove(row);
                    changed++;
                }
            }
            else if (row == null)
            {
                _context.ContentTranslations.Add(new ContentTranslation { EntityType = item.EntityType, EntityId = item.EntityId, Field = item.Field, Language = lang, Value = value });
                changed++;
            }
            else if (row.Value != value)
            {
                row.Value = value;
                changed++;
            }
        }

        if (changed > 0)
        {
            await _context.SaveChangesAsync();
            InvalidatePublicCaches();
            await _audit.LogAsync(User.FindFirstValue(ClaimTypes.NameIdentifier), User.Identity?.Name, "Çeviriler güncellendi",
                entityName: "ContentTranslation", details: $"{current.Title}: {changed} alan");
        }
        TempData["Info"] = changed > 0 ? $"{current.Title}: {changed} çeviri kaydedildi." : "Değişiklik yok.";
        return RedirectToAction(nameof(Index), new { tab = current.Key, missingOnly, q, page });
    }

    private void InvalidatePublicCaches()
    {
        SiteContentCache.Invalidate(_cache);
        MenuCache.Invalidate(_cache);
        HomePreviewCache.InvalidateGallery(_cache);
        HomePreviewCache.InvalidateCampaigns(_cache);
        HomePreviewCache.InvalidateHero(_cache);
        HomePreviewCache.InvalidateStats(_cache);
        HomePreviewCache.InvalidateFeatures(_cache);
        HomePreviewCache.InvalidateFeaturedMenu(_cache);
        HomePreviewCache.InvalidateChefSpecial(_cache);
        SeoCache.InvalidatePages(_cache);
        SeoCache.InvalidateFaqs(_cache);
        LegalPageCache.Invalidate(_cache);
    }

    private async Task<IReadOnlyList<TranslationRow>> LoadRowsAsync(TranslationTab tab)
    {
        var rows = new List<TranslationRow>();
        foreach (var type in tab.EntityTypes)
        {
            var entities = await LoadEntitiesAsync(type);
            var ids = entities.Select(e => e.Id).ToList();
            var english = await _context.ContentTranslations.AsNoTracking()
                .Where(t => t.EntityType == type && t.Language == TranslationCatalog.TargetLanguage && ids.Contains(t.EntityId))
                .ToDictionaryAsync(t => (t.EntityId, t.Field), t => t.Value);

            foreach (var (id, label, entity) in entities)
            {
                foreach (var field in TranslationOverlay.Fields[type])
                {
                    var prop = entity.GetType().GetProperty(field)!;
                    var turkish = prop.GetValue(entity) as string;
                    if (string.IsNullOrWhiteSpace(turkish))
                    {
                        continue;
                    }
                    rows.Add(new TranslationRow(type, id, label, field, prop.GetCustomAttribute<DisplayAttribute>()?.Name ?? field, turkish,
                        english.GetValueOrDefault((id, field)), TranslationCatalog.IsLong(field, turkish), MaxLength(type, field)));
                }
            }
        }
        return rows;
    }

    private async Task<List<(int Id, string Label, object Entity)>> LoadEntitiesAsync(string type) => type switch
    {
        "Category" => (await _context.Categories.AsNoTracking().OrderBy(c => c.DisplayOrder).ToListAsync()).Select(c => (c.Id, c.Name, (object)c)).ToList(),
        "MenuItem" => (await _context.MenuItems.AsNoTracking().Include(m => m.Category).OrderBy(m => m.Category!.DisplayOrder).ThenBy(m => m.Name).ToListAsync())
            .Select(m => (m.Id, $"{m.Category?.Name} › {m.Name}", (object)m)).ToList(),
        "MenuItemOption" => (await _context.MenuItemOptions.AsNoTracking().Include(o => o.MenuItem).OrderBy(o => o.MenuItem!.Name).ThenBy(o => o.Name).ToListAsync())
            .Select(o => (o.Id, $"{o.MenuItem?.Name} › {o.Name}", (object)o)).ToList(),
        "SiteContent" => (await _context.SiteContents.AsNoTracking().ToListAsync()).Select(s => (s.Id, "Site", (object)s)).ToList(),
        "HeroSlide" => (await _context.HeroSlides.AsNoTracking().OrderBy(h => h.DisplayOrder).ToListAsync()).Select(h => (h.Id, $"Slayt · {h.Title}", (object)h)).ToList(),
        "HomeStat" => (await _context.HomeStats.AsNoTracking().OrderBy(s => s.DisplayOrder).ToListAsync()).Select(s => (s.Id, $"İstatistik · {s.Label}", (object)s)).ToList(),
        "HomeFeature" => (await _context.HomeFeatures.AsNoTracking().OrderBy(f => f.DisplayOrder).ToListAsync()).Select(f => (f.Id, $"Özellik · {f.Title}", (object)f)).ToList(),
        "ChefSpecial" => (await _context.ChefSpecials.AsNoTracking().Include(c => c.MenuItem).OrderByDescending(c => c.StartDate).ToListAsync())
            .Select(c => (c.Id, $"Şefin önerisi · {c.MenuItem?.Name}", (object)c)).ToList(),
        "Faq" => (await _context.Faqs.AsNoTracking().OrderBy(f => f.DisplayOrder).ToListAsync()).Select(f => (f.Id, f.Question, (object)f)).ToList(),
        "Campaign" => (await _context.Campaigns.AsNoTracking().OrderByDescending(c => c.StartDate).ToListAsync()).Select(c => (c.Id, c.Name, (object)c)).ToList(),
        "GalleryImage" => (await _context.GalleryImages.AsNoTracking().OrderBy(g => g.DisplayOrder).ToListAsync()).Select(g => (g.Id, $"Fotoğraf #{g.Id}", (object)g)).ToList(),
        "LegalPage" => (await _context.LegalPages.AsNoTracking().OrderBy(p => p.Id).ToListAsync()).Select(p => (p.Id, p.Title, (object)p)).ToList(),
        "PageSeo" => (await _context.PageSeos.AsNoTracking().OrderBy(p => p.PageKey).ToListAsync())
            .Select(p => (p.Id, SeoPageCatalog.Find(p.PageKey)?.Label ?? p.PageKey, (object)p)).ToList(),
        _ => new()
    };

    private static int? MaxLength(string type, string field) =>
        typeof(ContentTranslation).Assembly.GetType($"{typeof(ContentTranslation).Namespace}.{type}")?.GetProperty(field)
            ?.GetCustomAttribute<StringLengthAttribute>()?.MaximumLength;
}
