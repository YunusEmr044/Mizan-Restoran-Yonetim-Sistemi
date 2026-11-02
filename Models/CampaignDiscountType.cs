using System.ComponentModel.DataAnnotations;

namespace RestoranYonetim.Models;

public enum CampaignDiscountType
{
    [Display(Name = "Yüzde")]
    Yuzde = 0,
    [Display(Name = "Sabit Tutar")]
    SabitTutar = 1
}
