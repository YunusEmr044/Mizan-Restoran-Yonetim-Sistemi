using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace RestoranYonetim.Extensions;

public static class EnumDisplayExtensions
{
    private static readonly ConcurrentDictionary<Enum, string> Cache = new();

    // Ekranda gösterilecek Türkçe ad: üyedeki [Display(Name)], yoksa kod adı. ToString() değişmez (form değerleri ve JS onu kullanır).
    public static string Label(this Enum value) =>
        Cache.GetOrAdd(value, v =>
            v.GetType().GetField(v.ToString())?.GetCustomAttribute<DisplayAttribute>()?.GetName() ?? v.ToString());
}
