using System.ComponentModel.DataAnnotations;

namespace RestoranYonetim.Models;

// Müşteri tarafı içeriğinin diğer dillerdeki karşılığı; bir kayıt türünün tek alanının tek dildeki metni.
public class ContentTranslation
{
    public const int EntityTypeMax = 50;
    public const int FieldMax = 50;
    public const int LanguageMax = 10;

    public int Id { get; set; }

    [StringLength(EntityTypeMax)]
    public string EntityType { get; set; } = string.Empty;

    public int EntityId { get; set; }

    [StringLength(FieldMax)]
    public string Field { get; set; } = string.Empty;

    [StringLength(LanguageMax)]
    public string Language { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;
}

// Panelden diğer dillere çevrilebilen metin alanı; çevirisi yoksa Türkçe değer gösterilir.
[AttributeUsage(AttributeTargets.Property)]
public sealed class TranslatableAttribute : Attribute
{
}
