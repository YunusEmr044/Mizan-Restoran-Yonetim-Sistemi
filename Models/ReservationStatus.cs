using System.ComponentModel.DataAnnotations;

namespace RestoranYonetim.Models;

public enum ReservationStatus
{
    [Display(Name = "Beklemede")]
    Beklemede = 0,
    [Display(Name = "Onaylandı")]
    Onaylandi = 1,
    [Display(Name = "Reddedildi")]
    Reddedildi = 2,
    [Display(Name = "İptal Edildi")]
    IptalEdildi = 3
}
