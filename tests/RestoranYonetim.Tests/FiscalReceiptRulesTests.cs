using RestoranYonetim.Models;
using RestoranYonetim.Services.Fiscal;

namespace RestoranYonetim.Tests;

public class FiscalReceiptRulesTests
{
    [Theory]
    [InlineData(FiscalReceiptStatus.Bekliyor, false, true)]
    [InlineData(FiscalReceiptStatus.Hata, false, true)]
    [InlineData(FiscalReceiptStatus.Belirsiz, true, true)]
    [InlineData(FiscalReceiptStatus.Kesildi, true, false)]
    [InlineData(FiscalReceiptStatus.Iptal, true, false)]
    [InlineData(FiscalReceiptStatus.FissizKapatildi, true, false)]
    [InlineData(FiscalReceiptStatus.ElleGirilecek, true, false)]
    public void Retry_Allowed_States(FiscalReceiptStatus status, bool confirmed, bool allowed)
    {
        Assert.Equal(allowed, FiscalReceiptRules.CanRetry(status, confirmed) == null);
    }

    [Fact]
    public void Uncertain_Needs_Confirmation()
    {
        Assert.NotNull(FiscalReceiptRules.CanRetry(FiscalReceiptStatus.Belirsiz, confirmedNotPrinted: false));
    }

    [Theory]
    [InlineData(FiscalReceiptStatus.Kesildi)]
    [InlineData(FiscalReceiptStatus.Iptal)]
    [InlineData(FiscalReceiptStatus.FissizKapatildi)]
    public void Closed_Receipt_Cannot_Change(FiscalReceiptStatus status)
    {
        Assert.NotNull(FiscalReceiptRules.CanEnterNumber(status, "123"));
        Assert.NotNull(FiscalReceiptRules.CanCloseWithoutReceipt(status, "gerekçe"));
    }

    [Fact]
    public void EnterNumber_Requires_Value()
    {
        Assert.NotNull(FiscalReceiptRules.CanEnterNumber(FiscalReceiptStatus.Belirsiz, " "));
        Assert.NotNull(FiscalReceiptRules.CanEnterNumber(FiscalReceiptStatus.Belirsiz, new string('1', 51)));
        Assert.Null(FiscalReceiptRules.CanEnterNumber(FiscalReceiptStatus.ElleGirilecek, "0042"));
    }

    [Fact]
    public void Close_Without_Receipt_Requires_Reason()
    {
        Assert.NotNull(FiscalReceiptRules.CanCloseWithoutReceipt(FiscalReceiptStatus.Hata, ""));
        Assert.Null(FiscalReceiptRules.CanCloseWithoutReceipt(FiscalReceiptStatus.Hata, "Fiş başka cihazdan kesildi"));
    }

    [Fact]
    public void Should_Create_Receipt()
    {
        Assert.False(FiscalReceiptRules.ShouldCreateReceipt(false, new FiscalDevice { IsActive = true }));
        Assert.False(FiscalReceiptRules.ShouldCreateReceipt(true, null));
        Assert.False(FiscalReceiptRules.ShouldCreateReceipt(true, new FiscalDevice { IsActive = false }));
        Assert.True(FiscalReceiptRules.ShouldCreateReceipt(true, new FiscalDevice { IsActive = true }));
    }

    [Fact]
    public void Open_Statuses_Exclude_Closed()
    {
        Assert.DoesNotContain(FiscalReceiptStatus.Kesildi, FiscalReceiptRules.OpenStatuses);
        Assert.Contains(FiscalReceiptStatus.Belirsiz, FiscalReceiptRules.OpenStatuses);
        Assert.Contains(FiscalReceiptStatus.ElleGirilecek, FiscalReceiptRules.OpenStatuses);
    }
}
