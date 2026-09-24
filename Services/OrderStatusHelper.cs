using RestoranYonetim.Models;

namespace RestoranYonetim.Services;

// Order.Status, kalemlerinin (OrderItem.Status) gerçek durumundan otomatik hesaplanan
// bir özet alandır. Mutfak/bar ekranları asıl OrderItem.Status üzerinde çalışır; bu
// yardımcı, bir kalem güncellendiğinde siparişin özet durumunu yeniden hesaplar.
public static class OrderStatusHelper
{
    public static void Recompute(Order order)
    {
        var activeItems = order.Items.Where(i => i.Status != OrderStatus.IptalEdildi).ToList();
        if (!activeItems.Any())
        {
            return;
        }

        if (activeItems.All(i => i.Status == OrderStatus.ServisEdildi))
        {
            order.Status = OrderStatus.ServisEdildi;
        }
        else if (activeItems.All(i => i.Status == OrderStatus.Hazir || i.Status == OrderStatus.ServisEdildi))
        {
            order.Status = OrderStatus.Hazir;
        }
        else if (activeItems.Any(i => i.Status == OrderStatus.Hazirlaniyor || i.Status == OrderStatus.Hazir))
        {
            order.Status = OrderStatus.Hazirlaniyor;
        }
        else
        {
            order.Status = OrderStatus.Yeni;
        }
    }
}
