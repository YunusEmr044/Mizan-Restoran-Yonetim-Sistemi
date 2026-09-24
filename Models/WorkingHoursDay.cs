using System.ComponentModel.DataAnnotations;

namespace RestoranYonetim.Models;

// Gün gün yapılandırılmış çalışma saatleri; "şu an açığız/kapalıyız" rozetinin otomatik hesaplanmasını sağlar.
// Yedi gün için sabit birer satır (DbSeeder ilk kurulumda oluşturur).
public class WorkingHoursDay
{
    public int Id { get; set; }

    [Display(Name = "Gün")]
    public DayOfWeek DayOfWeek { get; set; }

    [Display(Name = "Kapalı")]
    public bool IsClosed { get; set; }

    [Display(Name = "Açılış")]
    public TimeSpan OpenTime { get; set; } = new TimeSpan(10, 0, 0);

    [Display(Name = "Kapanış")]
    public TimeSpan CloseTime { get; set; } = new TimeSpan(22, 0, 0);
}
