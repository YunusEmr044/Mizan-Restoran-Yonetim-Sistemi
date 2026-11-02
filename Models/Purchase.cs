using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestoranYonetim.Models;

// Bir tedarikçiden yapılan satın alma kaydı - kalemleri stok girişine otomatik yansır.
public class Purchase
{
    public int Id { get; set; }

    [Display(Name = "Tedarikçi")]
    public int SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    [Display(Name = "Tarih")]
    public DateTime PurchaseDate { get; set; } = DateTime.Now;

    [StringLength(50)]
    [Display(Name = "Fatura No")]
    public string? InvoiceNumber { get; set; }

    public List<PurchaseItem> Items { get; set; } = new();

    [NotMapped]
    public decimal Total => Items.Sum(i => i.Quantity * i.UnitPrice);
}
