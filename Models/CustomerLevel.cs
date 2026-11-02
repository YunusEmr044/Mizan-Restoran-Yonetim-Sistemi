using System.ComponentModel.DataAnnotations;

namespace RestoranYonetim.Models;

public enum CustomerLevel
{
    [Display(Name = "Standart")]
    Standart = 0,
    [Display(Name = "Gümüş")]
    Silver = 1,
    [Display(Name = "Altın")]
    Gold = 2,
    [Display(Name = "VIP")]
    VIP = 3
}
