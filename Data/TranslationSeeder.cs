using Microsoft.EntityFrameworkCore;
using RestoranYonetim.Models;
using RestoranYonetim.Services.I18n;

namespace RestoranYonetim.Data;

// Varsayılan ve demo metinlerin İngilizcesini ekler: yalnızca çevirisi olmayan ve Türkçesi bilinen metne birebir eşit olan alanlara.
// Yasal sayfa gövdesi yalnızca hiç değiştirilmemiş Türkçe şablonla aynıysa İngilizce taslağı alır.
public static class TranslationSeeder
{
    public static async Task<int> SeedAsync(ApplicationDbContext context)
    {
        var language = TranslationCatalog.TargetLanguage;
        var existing = (await context.ContentTranslations.AsNoTracking().Where(t => t.Language == language)
                .Select(t => new { t.EntityType, t.EntityId, t.Field }).ToListAsync())
            .Select(t => (t.EntityType, t.EntityId, t.Field)).ToHashSet();
        var legalBodies = LegalPageTemplates.All.ToDictionary(t => Normalize(t.Body), t => LegalPageTemplates.English[t.Slug].Body);
        var added = 0;

        foreach (var (entityType, fields) in TranslationOverlay.Fields)
        {
            var clrType = typeof(ContentTranslation).Assembly.GetType($"{typeof(ContentTranslation).Namespace}.{entityType}");
            if (clrType == null)
            {
                continue;
            }
            var set = (IQueryable)context.GetType().GetMethod(nameof(DbContext.Set), Type.EmptyTypes)!.MakeGenericMethod(clrType).Invoke(context, null)!;
            var idProperty = clrType.GetProperty("Id")!;
            foreach (var entity in set.Cast<object>().ToList())
            {
                var id = (int)idProperty.GetValue(entity)!;
                foreach (var field in fields)
                {
                    if (clrType.GetProperty(field)?.GetValue(entity) is not string turkish || string.IsNullOrWhiteSpace(turkish)
                        || existing.Contains((entityType, id, field)))
                    {
                        continue;
                    }
                    var english = entityType == nameof(LegalPage) && field == nameof(LegalPage.Body)
                        ? legalBodies.GetValueOrDefault(Normalize(turkish))
                        : DemoTranslations.En.GetValueOrDefault(turkish);
                    if (english == null)
                    {
                        continue;
                    }
                    context.ContentTranslations.Add(new ContentTranslation { EntityType = entityType, EntityId = id, Field = field, Language = language, Value = english });
                    existing.Add((entityType, id, field));
                    added++;
                }
            }
        }
        if (added > 0)
        {
            await context.SaveChangesAsync();
        }
        return added;
    }

    // Kayıtlar farklı satır sonlarıyla (CRLF ya da LF) saklanmış olabilir.
    private static string Normalize(string text) => text.Replace("\r\n", "\n").Trim();
}
