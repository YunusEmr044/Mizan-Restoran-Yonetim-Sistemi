namespace RestoranYonetim.Models.Reports;

// Rapor ekranlarında kullanılan basit satır tipleri.
public class DailySalesRow
{
    public DateTime Date { get; set; }
    public decimal Total { get; set; }
    public int Count { get; set; }
}

public class PaymentBreakdownRow
{
    public PaymentMethod Method { get; set; }
    public decimal Total { get; set; }
}

public class ProductSalesRow
{
    public string Name { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal Revenue { get; set; }
}
