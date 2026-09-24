namespace RestoranYonetim.Models;

// Müşterinin "Garson Çağır" butonuna basmasıyla oluşan çağrı kaydı.
public class WaiterCall
{
    public int Id { get; set; }

    public int TableId { get; set; }
    public RestaurantTable? Table { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public bool Resolved { get; set; }
    public DateTime? ResolvedAt { get; set; }
}
