using System.ComponentModel.DataAnnotations;

namespace RestoranYonetim.Models;

public class GalleryImage
{
    public int Id { get; set; }

    [Required]
    [Display(Name = "Görsel")]
    public string ImageUrl { get; set; } = string.Empty;

    [StringLength(150)]
    [Display(Name = "Açıklama")]
    public string? Caption { get; set; }

    [Display(Name = "Sıra")]
    public int DisplayOrder { get; set; }

    [Display(Name = "Ana Sayfada Göster")]
    public bool ShowOnHomepage { get; set; }
}
