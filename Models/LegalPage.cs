using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;

namespace RestoranYonetim.Models;

// KVKK, gizlilik, çerez politikası ve kullanım koşulları sayfaları. Gövde Markdown'dır; {RestoranAdi} gibi alanlar görüntülemede doldurulur.
public class LegalPage
{
    public int Id { get; set; }

    [Required]
    [ValidateNever]
    [StringLength(60)]
    public string Slug { get; set; } = string.Empty;

    [Required(ErrorMessage = "Başlık gereklidir.")]
    [StringLength(150)]
    [Display(Name = "Başlık")]
    [Translatable]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Metin gereklidir.")]
    [Display(Name = "Metin")]
    [Translatable]
    public string Body { get; set; } = string.Empty;

    [Display(Name = "Yayında")]
    public bool IsPublished { get; set; } = true;

    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}
