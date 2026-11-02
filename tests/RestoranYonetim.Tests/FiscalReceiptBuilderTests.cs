using RestoranYonetim.Models;
using RestoranYonetim.Services.Fiscal;

namespace RestoranYonetim.Tests;

public class FiscalReceiptBuilderTests
{
    private static OrderItem Item(string name, decimal price, int qty = 1, int vat = 10, OrderStatus status = OrderStatus.ServisEdildi) =>
        new() { MenuItemName = name, UnitPrice = price, Quantity = qty, VatRate = vat, Status = status };

    private static Adisyon Adisyon(IEnumerable<OrderItem> items, params Payment[] payments) => new()
    {
        Id = 7,
        Orders = new List<Order> { new() { IsDraft = false, Status = OrderStatus.ServisEdildi, Items = items.ToList() } },
        Payments = payments.ToList()
    };

    private static Payment Pay(PaymentMethod method, decimal amount, int id = 0, int? reverses = null) =>
        new() { Id = id, Method = method, Amount = amount, ReversesPaymentId = reverses };

    [Fact]
    public void VatRate_Item_Overrides_Category()
    {
        Assert.Equal(20, VatRates.Resolve(20, 10));
        Assert.Equal(10, VatRates.Resolve(null, 10));
    }

    [Fact]
    public void VatRate_Default_Is_10()
    {
        Assert.Equal(10, VatRates.Default);
        Assert.True(VatRates.IsValid(1));
        Assert.False(VatRates.IsValid(18));
    }

    [Fact]
    public void Builds_Lines_With_Options_And_Vat()
    {
        var adana = Item("Adana Kebap", 380, 2);
        adana.Options.Add(new OrderItemOption { Name = "Lavaş", ExtraPrice = 10 });
        var raki = Item("Rakı", 300, vat: 20);

        var (request, error) = FiscalReceiptBuilder.Build(Adisyon(new[] { adana, raki }, Pay(PaymentMethod.Nakit, 1080)));

        Assert.Null(error);
        Assert.Equal(new[] { new FiscalLine("Adana Kebap", 2, 390, 10), new FiscalLine("Rakı", 1, 300, 20) }, request!.Lines);
        Assert.Equal(1080m, request.Total);
        Assert.Equal("ADS-7", request.Reference);
    }

    [Fact]
    public void Skips_Cancelled_Items()
    {
        var (request, _) = FiscalReceiptBuilder.Build(Adisyon(new[] { Item("Ayran", 70), Item("Kola", 90, status: OrderStatus.IptalEdildi) }, Pay(PaymentMethod.Nakit, 70)));

        Assert.Single(request!.Lines);
    }

    [Fact]
    public void Discount_Reduces_Total()
    {
        var a = Adisyon(new[] { Item("Adana", 400) }, Pay(PaymentMethod.Nakit, 360));
        a.DiscountAmount = 40;

        var (request, error) = FiscalReceiptBuilder.Build(a);

        Assert.Null(error);
        Assert.Equal((40m, 360m), (request!.Discount, request.Total));
    }

    [Fact]
    public void Split_Payment_Groups_By_Method()
    {
        var (request, _) = FiscalReceiptBuilder.Build(Adisyon(new[] { Item("Adana", 1000) },
            Pay(PaymentMethod.Nakit, 300), Pay(PaymentMethod.KrediKarti, 500), Pay(PaymentMethod.Nakit, 200)));

        Assert.Equal(new[] { new FiscalPayment(PaymentMethod.Nakit, 500), new FiscalPayment(PaymentMethod.KrediKarti, 500) }, request!.Payments);
    }

    [Fact]
    public void Reversed_And_Refunded_Payments_Excluded()
    {
        var (request, error) = FiscalReceiptBuilder.Build(Adisyon(new[] { Item("Adana", 500) },
            Pay(PaymentMethod.KrediKarti, 500, id: 1), Pay(PaymentMethod.KrediKarti, -500, id: 2, reverses: 1), Pay(PaymentMethod.Nakit, 500, id: 3)));

        Assert.Null(error);
        Assert.Equal(new[] { new FiscalPayment(PaymentMethod.Nakit, 500) }, request!.Payments);
    }

    [Fact]
    public void Mismatch_Returns_Error()
    {
        var (request, error) = FiscalReceiptBuilder.Build(Adisyon(new[] { Item("Adana", 500) }, Pay(PaymentMethod.Nakit, 300)));

        Assert.Null(request);
        Assert.NotNull(error);
    }

    [Fact]
    public void Long_Name_Truncated_To_32()
    {
        var (request, _) = FiscalReceiptBuilder.Build(Adisyon(new[] { Item(new string('a', 40), 10) }, Pay(PaymentMethod.Nakit, 10)));

        Assert.Equal(32, request!.Lines[0].Name.Length);
    }
}

public class VatValidationTests
{
    private static bool Valid(object model) =>
        System.ComponentModel.DataAnnotations.Validator.TryValidateObject(model, new System.ComponentModel.DataAnnotations.ValidationContext(model), null, true);

    [Fact]
    public void Category_And_Item_Vat_Validated()
    {
        Assert.True(Valid(new Category { Name = "Içecek", VatRate = 20 }));
        Assert.False(Valid(new Category { Name = "Içecek", VatRate = 18 }));
        Assert.True(Valid(new MenuItem { Name = "Ayran", VatRate = null }));
        Assert.False(Valid(new MenuItem { Name = "Ayran", VatRate = 8 }));
    }
}
