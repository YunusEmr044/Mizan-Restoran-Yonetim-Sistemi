using System.ComponentModel.DataAnnotations;

namespace RestoranYonetim.Models;

// Müşteri geri bildirimi (puanlama + yorum) - ödeme sonrası "Değerlendirme" linkiyle erişilen, girişsiz bir public form.
public class Feedback
{
    public int Id { get; set; }

    [StringLength(150)]
    [Display(Name = "Ad")]
    public string? Name { get; set; }

    [Range(1, 5, ErrorMessage = "1 ile 5 arasında bir puan seçin.")]
    [Display(Name = "Puan")]
    public int Rating { get; set; } = 5;

    [StringLength(1000)]
    [Display(Name = "Yorum")]
    public string? Comment { get; set; }

    public int? TableId { get; set; }
    public RestaurantTable? Table { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    // ÖNEMLİ: Form girişsiz/herkese açık olduğundan spam yorumlar otomatik yansımasın diye
    // varsayılan olarak YAYINDA DEĞİL; admin elle onaylayıp yayınlar.
    public bool IsPublished { get; set; } = false;

    // null = admin ana sayfa için açıkça seçmemiş; sayı verilirse ana sayfada gösterilir (küçük sayı önce gelir).
    [Display(Name = "Ana Sayfa Sırası")]
    public int? HomeDisplayOrder { get; set; }
}
