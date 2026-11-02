using System.ComponentModel.DataAnnotations;

namespace RestoranYonetim.Models;

public enum DiscountRequestStatus
{
    [Display(Name = "Beklemede")]
    Beklemede = 0,
    [Display(Name = "Onaylandı")]
    Onaylandi = 1,
    [Display(Name = "Reddedildi")]
    Reddedildi = 2
}
