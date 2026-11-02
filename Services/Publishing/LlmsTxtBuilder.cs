using System.Text;

namespace RestoranYonetim.Services.Publishing;

public sealed record LlmsTxtInput(
    string BaseUrl,
    string RestaurantName,
    string? Summary,
    string? Address,
    string? Phone,
    string? HoursText,
    IReadOnlyList<LlmsTxtPage> Pages,
    IReadOnlyList<string> Categories);

public sealed record LlmsTxtPage(string Label, string Path, string? Description);

// /llms.txt (llmstxt.org biçimi): yapay zekâ asistanlarının restoranı doğru tanıtması için kısa, yapılandırılmış özet.
public static class LlmsTxtBuilder
{
    public static string Build(LlmsTxtInput input)
    {
        var baseUrl = input.BaseUrl.TrimEnd('/');
        var sb = new StringBuilder();
        sb.Append("# ").Append(input.RestaurantName).Append('\n');

        if (!string.IsNullOrWhiteSpace(input.Summary))
        {
            sb.Append('\n').Append("> ").Append(input.Summary.Trim()).Append('\n');
        }

        var contact = new[]
        {
            Labeled("Adres", input.Address),
            Labeled("Telefon", input.Phone),
            Labeled("Çalışma saatleri", input.HoursText)
        }.Where(p => p != null);
        var contactLine = string.Join(" | ", contact);
        if (contactLine.Length > 0)
        {
            sb.Append('\n').Append(contactLine).Append('\n');
        }

        if (input.Pages.Count > 0)
        {
            sb.Append("\n## Sayfalar\n");
            foreach (var page in input.Pages)
            {
                sb.Append("- [").Append(page.Label).Append("](").Append(baseUrl).Append(page.Path).Append(')');
                if (!string.IsNullOrWhiteSpace(page.Description))
                {
                    sb.Append(": ").Append(page.Description);
                }
                sb.Append('\n');
            }
        }

        if (input.Categories.Count > 0)
        {
            sb.Append("\n## Menü kategorileri\n");
            foreach (var category in input.Categories)
            {
                sb.Append("- ").Append(category).Append('\n');
            }
        }

        return sb.ToString();
    }

    private static string? Labeled(string label, string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : $"{label}: {value.Trim()}";
}
