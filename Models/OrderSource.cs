using System.ComponentModel.DataAnnotations;

namespace RestoranYonetim.Models;

public enum OrderSource
{
    [Display(Name = "Müşteri (QR)")]
    Musteri = 0,
    [Display(Name = "Garson")]
    Garson = 1
}
