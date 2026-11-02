using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace RestoranYonetim.Models;

// Gel-al ve adrese teslim adisyonlarının müşteri ve teslimat bilgileri; Adisyonlar tablosunda "Package_" önekli sütunlarda tutulur.
[Owned]
public class PackageInfo
{
    public const int NameMax = 150;
    public const int PhoneMax = 20;
    public const int AddressMax = 500;
    public const int NoteMax = 300;
    public const int CourierMax = 100;

    [StringLength(NameMax)]
    public string? CustomerName { get; set; }

    [StringLength(PhoneMax)]
    public string? CustomerPhone { get; set; }

    [StringLength(AddressMax)]
    public string? Address { get; set; }

    [StringLength(NoteMax)]
    public string? Note { get; set; }

    public DateTime? PromisedAt { get; set; }

    [StringLength(CourierMax)]
    public string? CourierName { get; set; }

    public DateTime? HandedOverAt { get; set; }
    public DateTime? DispatchedAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
}
