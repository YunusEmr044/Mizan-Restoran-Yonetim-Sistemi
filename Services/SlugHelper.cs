using System.Text;

namespace RestoranYonetim.Services;

// Ürün/kampanya adı gibi bir metinden Türkçe karakterleri doğru çeviren, URL-güvenli bir slug üretir.
public static class SlugHelper
{
    private static readonly Dictionary<char, char> TurkishMap = new()
    {
        ['ç'] = 'c', ['Ç'] = 'c',
        ['ğ'] = 'g', ['Ğ'] = 'g',
        ['ı'] = 'i', ['I'] = 'i',
        ['İ'] = 'i',
        ['ö'] = 'o', ['Ö'] = 'o',
        ['ş'] = 's', ['Ş'] = 's',
        ['ü'] = 'u', ['Ü'] = 'u'
    };

    // "Kuzu Tandır (Özel)" -> "kuzu-tandir-ozel". Boş/anlamsız girişte null döner.
    public static string? Slugify(string? input, int maxLength = 60)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        var sb = new StringBuilder(input.Length);
        foreach (var ch in input.Trim())
        {
            if (TurkishMap.TryGetValue(ch, out var mapped))
            {
                sb.Append(mapped);
                continue;
            }

            if (char.IsLetterOrDigit(ch) && ch < 128)
            {
                sb.Append(char.ToLowerInvariant(ch));
            }
            else if (char.IsWhiteSpace(ch) || ch is '-' or '_')
            {
                sb.Append('-');
            }
        }

        var slug = string.Join('-', sb.ToString().Split('-', StringSplitOptions.RemoveEmptyEntries));
        if (slug.Length > maxLength)
        {
            slug = slug[..maxLength].TrimEnd('-');
        }

        return string.IsNullOrWhiteSpace(slug) ? null : slug;
    }

    // "<slug>-<kısa-guid><uzantı>" üretir; slug üretilemezse fallbackWord kullanılır.
    public static string BuildFileName(string? nameHint, string fallbackWord, string extension)
    {
        var slug = Slugify(nameHint) ?? fallbackWord;
        var shortId = Guid.NewGuid().ToString("N")[..8];
        return $"{slug}-{shortId}{extension}";
    }
}
