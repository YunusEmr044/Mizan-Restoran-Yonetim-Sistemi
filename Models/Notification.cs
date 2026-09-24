using System.ComponentModel.DataAnnotations;

namespace RestoranYonetim.Models;

// Bildirimler kullanıcı bazlı değil, RequiredPermission'ı karşılayan role genel yayın olarak düşer.
// ÖNEMLİ: okunma durumu bu sınıfta TUTULMAZ; tek bir paylaşılan IsRead bayrağı olsaydı bir kullanıcının
// "okundu" demesi aynı role sahip herkes için okunmuş sayardı. Her kullanıcının okunma durumu ayrı
// NotificationRead tablosunda tutulur.
public class Notification
{
    public int Id { get; set; }

    [Required]
    [StringLength(300)]
    public string Message { get; set; } = string.Empty;

    [StringLength(300)]
    public string? Link { get; set; }

    // Bu bildirimi görebilecek kullanıcıların sahip olması gereken izin (örn.
    // Permissions.Orders.View -> garson/mutfak/bar; Permissions.Cash.View -> kasa).
    [StringLength(100)]
    public string? RequiredPermission { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
