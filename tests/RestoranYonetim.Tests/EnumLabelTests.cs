using RestoranYonetim.Extensions;
using RestoranYonetim.Models;

namespace RestoranYonetim.Tests;

public class EnumLabelTests
{
    public static TheoryData<Type> DisplayedEnums => new()
    {
        typeof(OrderStatus), typeof(PaymentMethod), typeof(AdisyonStatus), typeof(ReservationStatus),
        typeof(CustomerLevel), typeof(InventoryTransactionType), typeof(TableStatus), typeof(OrderSource),
        typeof(MenuDepartment), typeof(DiscountRequestStatus), typeof(VoidRequestType), typeof(CampaignDiscountType)
    };

    [Theory]
    [MemberData(nameof(DisplayedEnums))]
    public void EveryMemberHasTurkishLabel(Type enumType)
    {
        foreach (Enum value in Enum.GetValues(enumType))
        {
            Assert.False(string.IsNullOrWhiteSpace(value.Label()), $"{enumType.Name}.{value}");
        }
    }

    [Fact]
    public void KnownLabels()
    {
        Assert.Equal("Kredi Kartı", PaymentMethod.KrediKarti.Label());
        Assert.Equal("Havale/EFT", PaymentMethod.HavaleEft.Label());
        Assert.Equal("Servis Edildi", OrderStatus.ServisEdildi.Label());
        Assert.Equal("Hazırlanıyor", OrderStatus.Hazirlaniyor.Label());
        Assert.Equal("Sipariş Bekliyor", TableStatus.SiparisBekliyor.Label());
        Assert.Equal("Satın Alma", InventoryTransactionType.SatinAlma.Label());
        Assert.Equal("Nakit", PaymentMethod.Nakit.Label());
    }

    [Fact]
    public void ToStringStaysTheCodeName()
    {
        Assert.Equal("KrediKarti", PaymentMethod.KrediKarti.ToString());
    }
}
