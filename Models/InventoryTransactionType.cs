using System.ComponentModel.DataAnnotations;

namespace RestoranYonetim.Models;

public enum InventoryTransactionType
{
    [Display(Name = "Satın Alma")]
    SatinAlma = 0,
    [Display(Name = "Satış Düşümü")]
    SatisDususu = 1,
    [Display(Name = "Manuel Düzeltme")]
    ManuelDuzeltme = 2,
    [Display(Name = "Fire")]
    Fire = 3,
    [Display(Name = "Sipariş İptali")]
    SiparisIptali = 4
}
