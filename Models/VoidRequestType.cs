using System.ComponentModel.DataAnnotations;

namespace RestoranYonetim.Models;

public enum VoidRequestType
{
    [Display(Name = "Kalem iptali")]
    KalemIptali = 0,
    [Display(Name = "Ödeme iptali")]
    OdemeIptali = 1,
    [Display(Name = "İade")]
    Iade = 2,
    [Display(Name = "Adisyon iptali")]
    AdisyonIptali = 3
}
