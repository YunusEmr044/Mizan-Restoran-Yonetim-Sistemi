using System.ComponentModel.DataAnnotations;

namespace RestoranYonetim.Models;

// Tedarikçi bilgileri: stok kalemleri ve satın alma kayıtları buraya bağlanır.
public class Supplier
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Firma adı gereklidir.")]
    [StringLength(150)]
    [Display(Name = "Firma Adı")]
    public string Name { get; set; } = string.Empty;

    [StringLength(150)]
    [Display(Name = "Yetkili Kişi")]
    public string? ContactName { get; set; }

    [StringLength(50)]
    [Display(Name = "Telefon")]
    public string? Phone { get; set; }

    [StringLength(150)]
    [Display(Name = "E-posta")]
    public string? Email { get; set; }

    [StringLength(50)]
    [Display(Name = "Vergi No")]
    public string? TaxNumber { get; set; }

    [StringLength(300)]
    [Display(Name = "Adres")]
    public string? Address { get; set; }

    public List<InventoryItem> InventoryItems { get; set; } = new();
    public List<Purchase> Purchases { get; set; } = new();
}
