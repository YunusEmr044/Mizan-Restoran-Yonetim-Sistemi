using System.ComponentModel.DataAnnotations;

namespace RestoranYonetim.Models;

public enum AdisyonStatus
{
    [Display(Name = "Açık")]
    Acik = 0,
    [Display(Name = "Hesap İstendi")]
    HesapIstendi = 1,
    [Display(Name = "Kasada")]
    Kasada = 2,
    [Display(Name = "Kısmi Ödendi")]
    KismiOdendi = 3,
    [Display(Name = "Ödendi")]
    Odendi = 4,
    [Display(Name = "İptal")]
    Iptal = 5,
    [Display(Name = "İade")]
    Iade = 6,
    [Display(Name = "Birleştirildi")]
    Birlestirildi = 7
}
