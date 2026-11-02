using RestoranYonetim.Models;
using RestoranYonetim.Services;

namespace RestoranYonetim.Tests;

public class ReceiptBuilderTests
{
    [Fact]
    public void PaymentLines_GroupsByMethodAndNetsReversals()
    {
        var payments = new[]
        {
            new Payment { Method = PaymentMethod.Nakit, Amount = 300 },
            new Payment { Method = PaymentMethod.KrediKarti, Amount = 500 },
            new Payment { Method = PaymentMethod.Nakit, Amount = 200 },
            new Payment { Method = PaymentMethod.KrediKarti, Amount = -500, ReversesPaymentId = 2 },
            new Payment { Method = PaymentMethod.BankaKarti, Amount = 500 }
        };

        var lines = ReceiptBuilder.PaymentLines(payments);

        Assert.Equal(new[] { ("Nakit", 500m), ("Banka Kartı", 500m) }, lines);
    }

    [Fact]
    public void PaymentLines_EmptyWhenNothingPaid()
    {
        Assert.Empty(ReceiptBuilder.PaymentLines([]));
    }

    [Fact]
    public void WaiterNames_DistinctFullNamesInOrder()
    {
        var names = ReceiptBuilder.WaiterNames(["u1", "u2", "u1", null, "u3"],
            new Dictionary<string, string?> { ["u1"] = "Ali Yılmaz", ["u2"] = "  ", ["u3"] = "Ece Kaya" });

        Assert.Equal("Ali Yılmaz, Ece Kaya", names);
    }

    [Fact]
    public void WaiterNames_NullWhenUnknown()
    {
        Assert.Null(ReceiptBuilder.WaiterNames([null, "x"], new Dictionary<string, string?>()));
    }
}
