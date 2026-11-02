using System.ComponentModel.DataAnnotations;

namespace RestoranYonetim.Models;

public enum OrderType
{
    [Display(Name = "Masa")]
    Masa = 0,
    [Display(Name = "Gel-Al")]
    GelAl = 1,
    [Display(Name = "Adrese Teslim")]
    AdreseTeslim = 2
}
