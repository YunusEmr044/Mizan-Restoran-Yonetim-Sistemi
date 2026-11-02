using System.ComponentModel.DataAnnotations;

namespace RestoranYonetim.Models;

// Adisyon/ödeme durumundan bağımsızdır (bkz. AdisyonStatus). Hem Order.Status (özet, kalemlerden otomatik
// hesaplanır) hem de OrderItem.Status (kalem bazlı gerçek durum) için kullanılır.
public enum OrderStatus
{
    [Display(Name = "Yeni")]
    Yeni = 0,
    [Display(Name = "Hazırlanıyor")]
    Hazirlaniyor = 1,
    [Display(Name = "Hazır")]
    Hazir = 2,
    [Display(Name = "Servis Edildi")]
    ServisEdildi = 3,
    [Display(Name = "İptal Edildi")]
    IptalEdildi = 4
}
