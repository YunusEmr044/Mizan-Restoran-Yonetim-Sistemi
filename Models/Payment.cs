using System.ComponentModel.DataAnnotations.Schema;

namespace RestoranYonetim.Models;

public class Payment
{
    public int Id { get; set; }

    public int AdisyonId { get; set; }
    public Adisyon? Adisyon { get; set; }

    public int? CashShiftId { get; set; }
    public CashShift? CashShift { get; set; }

    public PaymentMethod Method { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal Amount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public string? ReceivedByUserId { get; set; }
    public string? ReceivedByUserName { get; set; }
}
