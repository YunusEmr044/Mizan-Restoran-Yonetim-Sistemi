namespace RestoranYonetim.Models;

// Ürün bazlı bölünmüş ödemede bu ödemenin kapattığı kalem ve adet.
public class PaymentItem
{
    public int Id { get; set; }

    public int PaymentId { get; set; }
    public Payment? Payment { get; set; }

    public int OrderItemId { get; set; }
    public OrderItem? OrderItem { get; set; }

    public int Quantity { get; set; }
}
