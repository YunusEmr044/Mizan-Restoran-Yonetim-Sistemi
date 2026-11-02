namespace RestoranYonetim.Models;

// Her (Notification, User) çifti için en fazla bir satır var (bkz. ApplicationDbContext unique index);
// bir satırın varlığı o kullanıcının bildirimi okuduğu anlamına gelir.
public class NotificationRead
{
    public int Id { get; set; }

    public int NotificationId { get; set; }
    public Notification? Notification { get; set; }

    public string UserId { get; set; } = string.Empty;

    public DateTime ReadAt { get; set; } = DateTime.Now;
}
