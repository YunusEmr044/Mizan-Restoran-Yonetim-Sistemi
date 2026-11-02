using System.Globalization;
using System.Text;
using RestoranYonetim.Models;

namespace RestoranYonetim.Services;

public sealed record MenuCsvRow(int Line, string Category, string Name, decimal Price, string? Description, MenuDepartment Department, int? VatRate);
public sealed record MenuCsvError(int Line, string Message);
public sealed record MenuCsvResult(IReadOnlyList<MenuCsvRow> Rows, IReadOnlyList<MenuCsvError> Errors);

// Menüyü dosyadan içe aktarma: Excel'in kaydettiği ; ya da , ayraçlı CSV. Hatalı satır varsa hiçbir satır alınmaz.
public static class MenuCsv
{
    public const int MaxRows = 2000;
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");
    private static readonly int[] VatRates = { 0, 1, 10, 20 };

    public const string Template =
        "Kategori;Ürün;Fiyat;Açıklama;Bölüm;KDV\r\n" +
        "Çorbalar;Mercimek Çorbası;90,00;250 ml;Mutfak;10\r\n" +
        "Kebap ve Izgara;Adana Kebap;450,00;Acılı, közlenmiş domates ve biberle;Mutfak;\r\n" +
        "İçecekler;Ayran;40,00;300 ml;Bar;\r\n";

    public static MenuCsvResult Parse(string text)
    {
        var records = ReadRecords(text.TrimStart('﻿'));
        var errors = new List<MenuCsvError>();
        var rows = new List<MenuCsvRow>();
        if (records.Count == 0 || records[0].Count < 3
            || !records[0][0].Trim().StartsWith("Kategori", true, Tr) || !records[0][1].Trim().StartsWith("Ürün", true, Tr))
        {
            errors.Add(new MenuCsvError(1, "İlk satır başlık olmalı: Kategori;Ürün;Fiyat;Açıklama;Bölüm;KDV. Şablonu indirip onu doldurun."));
            return new MenuCsvResult(rows, errors);
        }
        if (records.Count - 1 > MaxRows)
        {
            errors.Add(new MenuCsvError(1, $"Tek seferde en fazla {MaxRows} ürün aktarılabilir."));
            return new MenuCsvResult(rows, errors);
        }

        var seen = new HashSet<string>(StringComparer.Create(Tr, true));
        for (var i = 1; i < records.Count; i++)
        {
            var line = i + 1;
            var cells = records[i];
            if (cells.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }
            string Cell(int index) => index < cells.Count ? cells[index].Trim() : string.Empty;
            var category = Cell(0);
            var name = Cell(1);
            var problems = new List<string>();
            if (category.Length is 0 or > 100) problems.Add("kategori boş ya da 100 karakterden uzun");
            if (name.Length is 0 or > 150) problems.Add("ürün adı boş ya da 150 karakterden uzun");
            if (!TryPrice(Cell(2), out var price)) problems.Add("fiyat 0 ile 100000 arasında bir sayı olmalı (ör. 125,50)");
            var description = Cell(3);
            if (description.Length > 500) problems.Add("açıklama 500 karakterden uzun");
            var departmentText = Cell(4);
            MenuDepartment department = MenuDepartment.Mutfak;
            if (departmentText.Length > 0 && !TryDepartment(departmentText, out department)) problems.Add("bölüm Mutfak ya da Bar olmalı");
            int? vat = null;
            var vatText = Cell(5).TrimStart('%');
            if (vatText.Length > 0)
            {
                if (int.TryParse(vatText, NumberStyles.None, CultureInfo.InvariantCulture, out var v) && VatRates.Contains(v)) vat = v;
                else problems.Add("KDV 0, 1, 10 ya da 20 olmalı");
            }
            if (problems.Count == 0 && !seen.Add(category + "\u0001" + name)) problems.Add("aynı kategoride bu ürün dosyada iki kez var");

            if (problems.Count > 0)
            {
                errors.Add(new MenuCsvError(line, $"{line}. satır: {string.Join("; ", problems)}."));
            }
            else
            {
                rows.Add(new MenuCsvRow(line, category, name, price, description.Length == 0 ? null : description, department, vat));
            }
        }
        if (rows.Count == 0 && errors.Count == 0)
        {
            errors.Add(new MenuCsvError(1, "Dosyada ürün satırı yok."));
        }
        return errors.Count > 0 ? new MenuCsvResult(Array.Empty<MenuCsvRow>(), errors) : new MenuCsvResult(rows, errors);
    }

    // "1.250,50", "1250,5" ve "1250.50" kabul edilir.
    private static bool TryPrice(string text, out decimal price)
    {
        var normalized = text.Replace("₺", "").Replace("TL", "", StringComparison.OrdinalIgnoreCase).Replace(" ", "");
        if (normalized.Contains(',') && normalized.Contains('.'))
        {
            normalized = normalized.Replace(".", "");
        }
        normalized = normalized.Replace(',', '.');
        return decimal.TryParse(normalized, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out price) && price is >= 0 and <= 100000;
    }

    private static bool TryDepartment(string text, out MenuDepartment department)
    {
        department = MenuDepartment.Mutfak;
        if (string.Equals(text, "Mutfak", StringComparison.OrdinalIgnoreCase)) return true;
        if (string.Equals(text, "Bar", StringComparison.OrdinalIgnoreCase))
        {
            department = MenuDepartment.Bar;
            return true;
        }
        return false;
    }

    // Tırnaklı alanları ("a;b", "" kaçışı) ve tırnak içindeki satır sonlarını destekleyen küçük bir CSV okuyucu. Ayraç başlık satırından seçilir.
    private static List<List<string>> ReadRecords(string text)
    {
        var firstLine = text.Split('\n')[0];
        var separator = firstLine.Count(c => c == ';') >= firstLine.Count(c => c == ',') ? ';' : ',';
        var records = new List<List<string>>();
        var record = new List<string>();
        var field = new StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (inQuotes)
            {
                if (c == '"' && i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                else if (c == '"') inQuotes = false;
                else field.Append(c);
            }
            else if (c == '"' && field.Length == 0) inQuotes = true;
            else if (c == separator) { record.Add(field.ToString()); field.Clear(); }
            else if (c == '\n')
            {
                record.Add(field.ToString().TrimEnd('\r'));
                field.Clear();
                records.Add(record);
                record = new List<string>();
            }
            else field.Append(c);
        }
        if (field.Length > 0 || record.Count > 0)
        {
            record.Add(field.ToString().TrimEnd('\r'));
            records.Add(record);
        }
        return records;
    }
}
