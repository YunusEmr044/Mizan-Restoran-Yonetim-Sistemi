using System.ComponentModel.DataAnnotations;

namespace RestoranYonetim.Models;

public enum PaymentMethod
{
    [Display(Name = "Nakit")]
    Nakit = 0,
    [Display(Name = "Kredi Kartı")]
    KrediKarti = 1,
    [Display(Name = "Banka Kartı")]
    BankaKarti = 2,
    [Display(Name = "Yemek Kartı")]
    YemekKarti = 3,
    [Display(Name = "Havale/EFT")]
    HavaleEft = 4
}
