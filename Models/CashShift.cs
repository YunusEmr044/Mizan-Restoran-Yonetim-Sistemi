using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace RestoranYonetim.Models;

// Kasa vardiyası: açılışta başlangıç tutarı girilir, kapanışta sayılan gerçek tutar
// girilir ve sistemin beklediği (başlangıç + nakit tahsilat) tutarla karşılaştırılıp
// kasa farkı hesaplanır.
public class CashShift
{
    public int Id { get; set; }

    public DateTime OpenedAt { get; set; } = DateTime.Now;
    public DateTime? ClosedAt { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal OpeningAmount { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal? ClosingCountedAmount { get; set; }

    public string? OpenedByUserId { get; set; }
    public string? OpenedByUserName { get; set; }
    public string? ClosedByUserId { get; set; }
    public string? ClosedByUserName { get; set; }

    // Optimistic concurrency (bkz. Adisyon.RowVersion) - iki kasiyer aynı vardiyayı aynı anda kapatırsa
    // ikinci istek DbUpdateConcurrencyException alır, sessizce üzerine yazmaz.
    [Timestamp]
    public byte[] RowVersion { get; set; } = null!;

    public List<Payment> Payments { get; set; } = new();

    [NotMapped]
    public bool IsOpen => ClosedAt == null;

    [NotMapped]
    public decimal CashPaymentsTotal => Payments.Where(p => p.Method == PaymentMethod.Nakit).Sum(p => p.Amount);

    [NotMapped]
    public decimal CardAndOtherPaymentsTotal => Payments.Where(p => p.Method != PaymentMethod.Nakit).Sum(p => p.Amount);

    [NotMapped]
    public decimal TotalSales => Payments.Sum(p => p.Amount);

    [NotMapped]
    public decimal ExpectedCash => OpeningAmount + CashPaymentsTotal;

    [NotMapped]
    public decimal? CashDifference => ClosingCountedAmount.HasValue ? ClosingCountedAmount.Value - ExpectedCash : null;
}
