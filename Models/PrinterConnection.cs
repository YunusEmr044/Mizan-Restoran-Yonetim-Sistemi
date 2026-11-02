using System.ComponentModel.DataAnnotations;

namespace RestoranYonetim.Models;

public enum PrinterConnection
{
    [Display(Name = "Tarayıcı")]
    Tarayici = 0,
    [Display(Name = "Ağ")]
    Ag = 1
}
