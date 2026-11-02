using System.Globalization;

namespace RestoranYonetim.Services;

// Tutar girişleri: tarayıcının number alanı "12.50", Türkçe yazım "12,50" gönderir; sunucu tr-TR çalıştığı için varsayılan
// bağlayıcı noktayı binlik ayıracı sayıp 1250 okuyordu. Son ayıraç ondalık kabul edilir, diğeri binlik olarak atılır.
public static class DecimalInput
{
    public static bool TryParse(string? input, out decimal value)
    {
        value = 0;
        var text = input?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        var lastDot = text.LastIndexOf('.');
        var lastComma = text.LastIndexOf(',');
        if (lastDot >= 0 && lastComma >= 0)
        {
            var decimalSeparator = lastDot > lastComma ? '.' : ',';
            var thousandsSeparator = decimalSeparator == '.' ? ',' : '.';
            text = text.Replace(thousandsSeparator.ToString(), string.Empty);
            if (text.Count(c => c == decimalSeparator) > 1)
            {
                return false;
            }
            text = text.Replace(decimalSeparator, '.');
        }
        else if (lastDot >= 0 || lastComma >= 0)
        {
            var separator = lastDot >= 0 ? '.' : ',';
            text = text.Count(c => c == separator) > 1
                ? text.Replace(separator.ToString(), string.Empty)
                : text.Replace(separator, '.');
        }

        return decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value);
    }
}
