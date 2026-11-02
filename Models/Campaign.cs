using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestoranYonetim.Models;

// Kampanya: admin panelden tanımlanır, tarih aralığında aktifse public sitede duyurulur.
// Siparişe otomatik indirim uygulanmaz; kasa/garson tarafında manuel indirim talebi için referans niteliğindedir.
public class Campaign : IValidatableObject
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Kampanya adı gereklidir.")]
    [StringLength(150)]
    [Display(Name = "Ad")]
    [Translatable]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    [Display(Name = "Açıklama")]
    [Translatable]
    public string? Description { get; set; }

    [Display(Name = "İndirim Türü")]
    public CampaignDiscountType DiscountType { get; set; } = CampaignDiscountType.Yuzde;

    [Column(TypeName = "decimal(10,2)")]
    [Display(Name = "Değer")]
    public decimal Value { get; set; }

    [Display(Name = "Başlangıç")]
    public DateTime StartDate { get; set; } = DateTime.Today;

    [Display(Name = "Bitiş")]
    public DateTime EndDate { get; set; } = DateTime.Today.AddDays(7);

    [Display(Name = "Aktif")]
    public bool IsActive { get; set; } = true;

    // DisplayOrder'a göre en küçük olan "büyük" kampanya, diğerleri küçük kartlar olarak gösterilir.
    [Display(Name = "Görsel")]
    public string? ImageUrl { get; set; }

    [StringLength(80)]
    [Display(Name = "Buton Metni")]
    [Translatable]
    public string? ButtonText { get; set; }

    [StringLength(300)]
    [Display(Name = "Buton Linki")]
    public string? ButtonUrl { get; set; }

    [Display(Name = "Sıra")]
    public int DisplayOrder { get; set; }

    [NotMapped]
    public bool IsCurrentlyRunning => IsActive && DateTime.Today >= StartDate.Date && DateTime.Today <= EndDate.Date;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Value < 0)
        {
            yield return new ValidationResult("İndirim değeri negatif olamaz.", new[] { nameof(Value) });
        }
        if (DiscountType == CampaignDiscountType.Yuzde && Value > 100)
        {
            yield return new ValidationResult("Yüzde indirim en fazla 100 olabilir.", new[] { nameof(Value) });
        }
        if (EndDate.Date < StartDate.Date)
        {
            yield return new ValidationResult("Bitiş tarihi başlangıç tarihinden önce olamaz.", new[] { nameof(EndDate) });
        }
    }
}
