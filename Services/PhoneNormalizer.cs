namespace RestoranYonetim.Services;

// ÖNEMLİ: Telefonu TR numaraları için kanonik forma (10 hane, alan kodu + numara) indirger.
// Aynı müşteri farklı formatlarda girilirse (ör. "0532..." / "+90 532...") normalize edilmeden
// birebir string karşılaştırma yapılırsa sadakat/harcama geçmişi ayrı kayıtlara bölünür.
// NOT: geçmişte tutarsız kaydedilmiş eski kayıtları geriye dönük birleştirmez.
public static class PhoneNormalizer
{
    public static string Normalize(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return string.Empty;
        }

        var digits = new string(phone.Where(char.IsDigit).ToArray());

        // "+90 532 ..." veya "0090532..." gibi ülke kodu önekini at.
        if (digits.Length > 10 && digits.StartsWith("90"))
        {
            digits = digits[2..];
        }

        // "0532..." gibi yurt içi trunk prefix'ini at.
        if (digits.Length == 11 && digits.StartsWith("0"))
        {
            digits = digits[1..];
        }

        // Son 10 haneyi kanonik değer olarak kullan.
        if (digits.Length > 10)
        {
            digits = digits[^10..];
        }

        return digits;
    }
}
