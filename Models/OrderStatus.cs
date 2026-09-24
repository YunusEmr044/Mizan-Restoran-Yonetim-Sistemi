namespace RestoranYonetim.Models;

// Adisyon/ödeme durumundan bağımsızdır (bkz. AdisyonStatus). Hem Order.Status (özet, kalemlerden otomatik
// hesaplanır) hem de OrderItem.Status (kalem bazlı gerçek durum) için kullanılır.
public enum OrderStatus
{
    Yeni = 0,
    Hazirlaniyor = 1,
    Hazir = 2,
    ServisEdildi = 3,
    IptalEdildi = 4
}
