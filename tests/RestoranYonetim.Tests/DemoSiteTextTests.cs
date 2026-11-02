using RestoranYonetim.Data;
using RestoranYonetim.Models;

namespace RestoranYonetim.Tests;

public class DemoSiteTextTests
{
    private static SiteContent Placeholder() => new()
    {
        HeroTitle = "Restoranımıza Hoş Geldiniz",
        HeroSubtitle = "Taze malzemeler, güler yüzlü hizmet.",
        AboutText = "Bu metni admin panelindeki \"Site İçeriği\" bölümünden düzenleyebilirsiniz.",
        ContactPhone = "0000 000 00 00",
        ContactAddress = "Adresinizi admin panelinden girin."
    };

    [Fact]
    public void ReplacesPlaceholdersAndEmptyFields()
    {
        var content = Placeholder();

        Assert.True(DemoDataSeeder.ApplyDemoTexts(content));

        Assert.Equal("Örnek Ocakbaşı", content.HeroTitle);
        Assert.DoesNotContain("admin panel", content.AboutText);
        Assert.NotEqual("0000 000 00 00", content.ContactPhone);
        Assert.Contains("Kadıköy", content.ContactAddress);
        Assert.False(string.IsNullOrWhiteSpace(content.ContactEmail));
        Assert.Contains("output=embed", content.MapEmbedUrl);
        Assert.Equal(DemoImages.HeroSlides[0].Url, content.HeroImageUrl);
        Assert.Equal(DemoImages.About, content.AboutImageUrl);
        Assert.Equal(DemoImages.ReservationCta, content.ReservationCtaImageUrl);
        Assert.Equal(DemoImages.QrPhone, content.QrPhoneImageUrl);
    }

    [Fact]
    public void KeepsUserEditedFields()
    {
        var content = Placeholder();
        content.HeroTitle = "Mizan";
        content.AboutText = "Kendi hikâyemiz.";
        content.ContactAddress = "Taksim Meydanı, Beyoğlu, İstanbul";
        content.MapEmbedUrl = "https://www.google.com/maps?q=Taksim&output=embed";
        content.HeroImageUrl = "/uploads/site/kapak.webp";

        DemoDataSeeder.ApplyDemoTexts(content);

        Assert.Equal("Mizan", content.HeroTitle);
        Assert.Equal("Kendi hikâyemiz.", content.AboutText);
        Assert.Equal("Taksim Meydanı, Beyoğlu, İstanbul", content.ContactAddress);
        Assert.Equal("https://www.google.com/maps?q=Taksim&output=embed", content.MapEmbedUrl);
        Assert.Equal("/uploads/site/kapak.webp", content.HeroImageUrl);
    }

    [Fact]
    public void SecondCallChangesNothing()
    {
        var content = Placeholder();
        DemoDataSeeder.ApplyDemoTexts(content);

        Assert.False(DemoDataSeeder.ApplyDemoTexts(content));
    }
}
