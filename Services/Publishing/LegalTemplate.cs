using System.Text;
using RestoranYonetim.Models;

namespace RestoranYonetim.Services.Publishing;

public sealed record LegalTemplateValues(string? RestaurantName, string? Address, string? Email, string? Phone, string? SiteUrl)
{
    public static LegalTemplateValues From(SiteContent content, string siteUrl) =>
        new(content.HeroTitle, content.ContactAddress, content.ContactEmail, content.ContactPhone, siteUrl);
}

// Yasal sayfa Markdown şablonundaki {RestoranAdi} gibi alanları site bilgileriyle doldurur.
public static class LegalTemplate
{
    private const string EmptyValue = "—";
    private const string MarkdownSpecialChars = "\\*_[]#<>`";

    public static string Fill(string markdown, LegalTemplateValues values)
    {
        return markdown
            .Replace("{RestoranAdi}", Escape(values.RestaurantName))
            .Replace("{Adres}", Escape(values.Address))
            .Replace("{Eposta}", Escape(values.Email))
            .Replace("{Telefon}", Escape(values.Phone))
            .Replace("{SiteAdresi}", Escape(values.SiteUrl));
    }

    private static string Escape(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return EmptyValue;
        }

        var sb = new StringBuilder(value.Length);
        foreach (var c in value.Trim())
        {
            if (MarkdownSpecialChars.Contains(c))
            {
                sb.Append('\\');
            }
            sb.Append(c);
        }
        return sb.ToString();
    }
}
