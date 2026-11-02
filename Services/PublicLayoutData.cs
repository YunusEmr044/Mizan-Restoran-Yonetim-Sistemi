using RestoranYonetim.Models;

namespace RestoranYonetim.Services;

// _PublicLayout'un ihtiyaç duyduğu site bilgilerini ViewBag'e yazar. Herkese açık her sayfa bunu çağırmalı.
public static class PublicLayoutData
{
    public static void Apply(dynamic viewBag, SiteContent content)
    {
        viewBag.RestaurantName = content.HeroTitle;
        viewBag.LogoImageUrl = content.LogoImageUrl;
        viewBag.SeoTitle = content.SeoTitle;
        viewBag.SeoDescription = content.SeoDescription;
        viewBag.ContactPhone = content.ContactPhone;
        viewBag.ContactEmail = content.ContactEmail;
        viewBag.ContactAddress = content.ContactAddress;
        viewBag.WhatsAppNumber = content.WhatsAppNumber;
        viewBag.WhatsAppDefaultMessage = content.WhatsAppDefaultMessage;
        viewBag.WorkingHours = content.WorkingHours;
        viewBag.FooterText = content.FooterText;
        viewBag.FooterTagline = content.FooterTagline;
        viewBag.FacebookUrl = content.FacebookUrl;
        viewBag.InstagramUrl = content.InstagramUrl;
        viewBag.InstagramHandle = content.InstagramHandle;
        // Değerler ThemeCatalog.Safe*() ile doğrulanır: DB'de bozuk bir değer olsa bile site geçerli bir görünümle açılır.
        ThemeCatalog.ApplyToViewBag(viewBag, content);
    }
}
