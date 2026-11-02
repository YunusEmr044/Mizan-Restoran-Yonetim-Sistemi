using System.Globalization;
using RestoranYonetim.Models;
using RestoranYonetim.Security;

namespace RestoranYonetim.Services;

public static class PackageValidation
{
    // Paket türünü açan modülün izni; modül kapalıysa bu izin kullanıcıdan düşer.
    public static string? RequiredPermission(OrderType type) => type switch
    {
        OrderType.GelAl => Permissions.Takeaway.Create,
        OrderType.AdreseTeslim => Permissions.Delivery.Create,
        _ => null
    };

    public static string? FeatureKey(OrderType type) => type switch
    {
        OrderType.GelAl => "takeaway",
        OrderType.AdreseTeslim => "delivery",
        _ => null
    };

    // "SS:dd" teslim saati; geçmiş bir saatse ertesi güne yazılır.
    public static DateTime? PromisedAt(string? time, DateTime now)
    {
        if (!TimeSpan.TryParseExact(time?.Trim(), @"hh\:mm", CultureInfo.InvariantCulture, out var t))
        {
            return null;
        }
        var at = now.Date + t;
        return at < now ? at.AddDays(1) : at;
    }

    public static string? Validate(OrderType type, PackageInfo info)
    {
        if (type == OrderType.Masa)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(info.CustomerName))
        {
            return "Müşteri adını girin.";
        }

        if (type == OrderType.AdreseTeslim)
        {
            if (string.IsNullOrWhiteSpace(info.CustomerPhone))
            {
                return "Adrese teslim için telefon gereklidir.";
            }
            if (string.IsNullOrWhiteSpace(info.Address))
            {
                return "Adrese teslim için adres gereklidir.";
            }
        }

        return TooLong(info.CustomerName, PackageInfo.NameMax, "Müşteri adı")
            ?? TooLong(info.CustomerPhone, PackageInfo.PhoneMax, "Telefon")
            ?? TooLong(info.Address, PackageInfo.AddressMax, "Adres")
            ?? TooLong(info.Note, PackageInfo.NoteMax, "Not")
            ?? TooLong(info.CourierName, PackageInfo.CourierMax, "Kurye adı");
    }

    private static string? TooLong(string? value, int max, string field) =>
        value != null && value.Trim().Length > max ? $"{field} en fazla {max} karakter olabilir." : null;
}
