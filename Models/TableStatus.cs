using System.ComponentModel.DataAnnotations;

namespace RestoranYonetim.Models;

public enum TableStatus
{
    [Display(Name = "Boş")]
    Bos = 0,
    [Display(Name = "Dolu")]
    Dolu = 1,
    [Display(Name = "Rezerve")]
    Rezerve = 2,
    [Display(Name = "Sipariş Bekliyor")]
    SiparisBekliyor = 3,
    [Display(Name = "Hesap İstendi")]
    HesapIstendi = 4,
    [Display(Name = "Temizleniyor")]
    Temizleniyor = 5
}
