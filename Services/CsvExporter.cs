using System.Text;

namespace RestoranYonetim.Services;

// RFC4180 uyumlu CSV kaçışlama. ÖNEMLİ: UTF-8 BOM eklenir, aksi halde Excel dosyayı
// Windows-1252 gibi açıp Türkçe karakterleri (ş, ğ, İ) bozabilir.
public static class CsvExporter
{
    public static byte[] Build(IEnumerable<string> headers, IEnumerable<IEnumerable<object?>> rows)
    {
        var sb = new StringBuilder();
        sb.Append(string.Join(",", headers.Select(Escape)));
        sb.Append("\r\n");

        foreach (var row in rows)
        {
            sb.Append(string.Join(",", row.Select(v => Escape(v?.ToString() ?? string.Empty))));
            sb.Append("\r\n");
        }

        var bom = Encoding.UTF8.GetPreamble();
        var body = Encoding.UTF8.GetBytes(sb.ToString());
        var result = new byte[bom.Length + body.Length];
        Buffer.BlockCopy(bom, 0, result, 0, bom.Length);
        Buffer.BlockCopy(body, 0, result, bom.Length, body.Length);
        return result;
    }

    private static string Escape(string value)
    {
        if (value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0)
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
        return value;
    }
}
