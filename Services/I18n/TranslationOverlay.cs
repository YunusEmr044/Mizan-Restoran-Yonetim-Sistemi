using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RestoranYonetim.Data;
using RestoranYonetim.Models;

namespace RestoranYonetim.Services.I18n;

// [Translatable] alanların listesi ve çevirinin bir nesneye uygulanması. Yalnızca boş olmayan ve listede olan alanlar yazılır.
public static class TranslationOverlay
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyDictionary<string, PropertyInfo>> Properties =
        typeof(ContentTranslation).Assembly.GetTypes()
            .Where(t => t.Namespace == typeof(ContentTranslation).Namespace && t.IsClass)
            .Select(t => (Type: t, Props: t.GetProperties().Where(p => p.GetCustomAttribute<TranslatableAttribute>() != null && p.CanWrite).ToList()))
            .Where(x => x.Props.Count > 0)
            .ToDictionary(x => x.Type.Name, x => (IReadOnlyDictionary<string, PropertyInfo>)x.Props.ToDictionary(p => p.Name));

    public static IReadOnlyDictionary<string, IReadOnlyList<string>> Fields { get; } =
        Properties.ToDictionary(p => p.Key, p => (IReadOnlyList<string>)p.Value.Keys.ToList());

    public static bool IsTranslatable(string entityType, string field) =>
        Properties.TryGetValue(entityType, out var props) && props.ContainsKey(field);

    public static void Apply(object entity, IReadOnlyDictionary<string, string> values)
    {
        if (!Properties.TryGetValue(entity.GetType().Name, out var props))
        {
            return;
        }
        foreach (var (field, value) in values)
        {
            if (!string.IsNullOrWhiteSpace(value) && props.TryGetValue(field, out var prop))
            {
                prop.SetValue(entity, value);
            }
        }
    }
}

// Önbellek anahtarı dile göre ayrılır; içerik değişince her dilin anahtarı silinir.
public static class CacheKeys
{
    public static string For(string key, string lang) => key + ":" + lang;

    public static string For(string key) => For(key, SiteLanguages.Current);

    public static IEnumerable<string> AllLanguages(string key) => SiteLanguages.All.Select(l => For(key, l));

    public static void Remove(IMemoryCache cache, string key)
    {
        foreach (var k in AllLanguages(key))
        {
            cache.Remove(k);
        }
    }
}

// Önbelleğe girmeden önce, veritabanından yeni okunmuş (takipsiz) nesnelere geçerli dilin çevirisini uygular.
public static class ContentTranslator
{
    public static Task ApplyAsync(ApplicationDbContext context, params object?[] entities) =>
        ApplyAsync(context, SiteLanguages.Current, entities.Where(e => e != null)!);

    public static Task ApplyAsync<T>(ApplicationDbContext context, IEnumerable<T> entities) where T : class =>
        ApplyAsync(context, SiteLanguages.Current, entities);

    public static async Task ApplyAsync(ApplicationDbContext context, string lang, IEnumerable<object> entities)
    {
        if (lang == SiteLanguages.Default)
        {
            return;
        }

        var byType = entities.GroupBy(e => e.GetType().Name).Where(g => TranslationOverlay.Fields.ContainsKey(g.Key)).ToList();
        foreach (var group in byType)
        {
            var items = group.ToList();
            if (items.Any(e => context.Entry(e).State != EntityState.Detached))
            {
                throw new InvalidOperationException($"{group.Key}: çeviri yalnızca takip edilmeyen nesnelere uygulanır; aksi halde çevrilmiş metin Türkçe alana kaydedilebilir.");
            }

            var ids = items.Select(Id).ToList();
            var rows = await context.ContentTranslations.AsNoTracking()
                .Where(t => t.EntityType == group.Key && t.Language == lang && ids.Contains(t.EntityId))
                .Select(t => new { t.EntityId, t.Field, t.Value })
                .ToListAsync();
            var byEntity = rows.ToLookup(r => r.EntityId);
            foreach (var item in items)
            {
                TranslationOverlay.Apply(item, byEntity[Id(item)].ToDictionary(r => r.Field, r => r.Value));
            }
        }
    }

    private static int Id(object entity) => (int)entity.GetType().GetProperty("Id")!.GetValue(entity)!;
}
