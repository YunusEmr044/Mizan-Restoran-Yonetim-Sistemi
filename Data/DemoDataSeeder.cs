using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RestoranYonetim.Extensions;
using RestoranYonetim.Models;

namespace RestoranYonetim.Data;

// Yalnızca Development ortamında çağrılır: "Örnek Ocakbaşı" vitrin içeriğini ve demo işletme verisini yazar.
// Yalnızca müşteri tablosu boşken bir kez çalışır ve elle değiştirilmiş alanlara dokunmaz; sonradan silinen demo içerik geri gelmez.
public static class DemoDataSeeder
{
    private const string DemoName = "Örnek Ocakbaşı";

    public static readonly string[] PlaceholderTexts =
    {
        "Restoranımıza Hoş Geldiniz",
        "Taze malzemeler, güler yüzlü hizmet.",
        "Bu metni admin panelindeki \"Site İçeriği\" bölümünden düzenleyebilirsiniz.",
        "0000 000 00 00",
        "Adresinizi admin panelinden girin."
    };

    private const string AboutText =
        "Örnek Ocakbaşı, mahallenin kalabalık sofralarını özleyenler için açıldı. Etimizi her sabah kendi kasabımızdan alıyor, " +
        "kıymamızı ustamızın gözü önünde zırhla çekiyoruz.\n\n" +
        "Kebaplarımız meşe kömüründe, közün başında pişer; lahmacun ve pidelerimiz taş fırından sıcak gelir. Mezelerimizi ve " +
        "ev yemeklerimizi annelerimizden öğrendiğimiz tariflerle her gün yeniden hazırlıyoruz.\n\n" +
        "Salonda ya da bahçede, ister hızlı bir öğle yemeği ister uzun bir akşam sofrası olsun, çayınız bizden.";

    public static bool ApplyDemoTexts(SiteContent content)
    {
        var changed = false;
        string? Fill(string? current, string value)
        {
            if (!string.IsNullOrWhiteSpace(current) && !PlaceholderTexts.Contains(current)) return current;
            changed = true;
            return value;
        }

        content.HeroTitle = Fill(content.HeroTitle, DemoName)!;
        content.HeroSubtitle = Fill(content.HeroSubtitle, "Meşe kömüründe ocakbaşı lezzetleri, taş fırın ve ev yemekleri.");
        content.AboutText = Fill(content.AboutText, AboutText);
        content.ContactPhone = Fill(content.ContactPhone, "0216 555 34 12");
        content.ContactEmail = Fill(content.ContactEmail, "merhaba@ornekocakbasi.test");
        content.ContactAddress = Fill(content.ContactAddress, "Caferağa Mah. Moda Cad. No: 42, Kadıköy / İstanbul");
        content.MapEmbedUrl = Fill(content.MapEmbedUrl, "https://www.google.com/maps?q=Moda+Caddesi,+Kad%C4%B1k%C3%B6y,+%C4%B0stanbul&output=embed");
        content.FooterTagline = Fill(content.FooterTagline, "Közün başında, ustanın elinden.");
        content.HeroImageUrl = Fill(content.HeroImageUrl, DemoImages.HeroSlides[0].Url);
        content.AboutImageUrl = Fill(content.AboutImageUrl, DemoImages.About);
        content.ReservationCtaImageUrl = Fill(content.ReservationCtaImageUrl, DemoImages.ReservationCta);
        content.QrPhoneImageUrl = Fill(content.QrPhoneImageUrl, DemoImages.QrPhone);

        return changed;
    }

    public static async Task SeedAsync(ApplicationDbContext context, ILogger logger)
    {
        if (await context.Customers.AnyAsync())
        {
            return;
        }

        var content = await context.SiteContents.FirstOrDefaultAsync();
        if (content != null && ApplyDemoTexts(content))
        {
            await context.SaveChangesAsync();
        }

        var restaurant = await context.Restaurants.FirstOrDefaultAsync(r => r.Name == "Örnek Restoran");
        if (restaurant != null)
        {
            restaurant.Name = DemoName;
            await context.SaveChangesAsync();
        }

        await SeedGardenAsync(context);
        await SeedShowcaseAsync(context);

        var menu = await context.MenuItems.Include(m => m.Category).AsNoTracking().ToListAsync();
        var busyTables = await context.Adisyonlar
            .Where(a => a.Status != AdisyonStatus.Odendi && a.Status != AdisyonStatus.Iptal && a.Status != AdisyonStatus.Iade)
            .Select(a => a.TableId).ToListAsync();
        // Bugünün açık adisyonları listenin başındaki masalara düşer; mevcut açık adisyonu olan masalar sona alınır.
        var tables = (await context.RestaurantTables.OrderBy(t => t.Id).ToListAsync())
            .OrderBy(t => busyTables.Contains(t.Id)).ToList();
        if (menu.Count == 0 || tables.Count < 3)
        {
            logger.LogWarning("Demo işletme verisi atlandı: menü ya da masa yok.");
            return;
        }

        var data = DemoDataPlan.Build(DateTime.Now, menu, tables);
        await context.ExecuteInTransactionAsync(() =>
        {
            context.Suppliers.AddRange(data.Suppliers);
            context.InventoryItems.AddRange(data.Inventory);
            context.RecipeItems.AddRange(data.Recipes);
            context.Purchases.AddRange(data.Purchases);
            context.InventoryTransactions.AddRange(data.InventoryTransactions);
            context.CashShifts.AddRange(data.Shifts);
            context.Adisyonlar.AddRange(data.Adisyonlar);
            context.DiscountRequests.AddRange(data.DiscountRequests);
            context.WaiterCalls.AddRange(data.WaiterCalls);
            context.Customers.AddRange(data.Customers);
            context.Reservations.AddRange(data.Reservations);
            context.Feedbacks.AddRange(data.Feedbacks);
            context.Campaigns.AddRange(data.Campaigns);
            context.ChefSpecials.AddRange(data.ChefSpecials);
            foreach (var table in tables.Where(t => data.TableStatuses.ContainsKey(t.Id)))
            {
                table.Status = data.TableStatuses[table.Id];
            }
            return Task.CompletedTask;
        });

        logger.LogInformation("Demo işletme verisi yazıldı: {Adisyon} adisyon, {Customer} müşteri.", data.Adisyonlar.Count, data.Customers.Count);
    }

    private static async Task SeedGardenAsync(ApplicationDbContext context)
    {
        if (await context.Areas.AnyAsync(a => a.Name == "Bahçe"))
        {
            return;
        }

        var salon = await context.Areas.OrderBy(a => a.Id).FirstOrDefaultAsync();
        if (salon == null)
        {
            return;
        }

        var garden = new Area { Name = "Bahçe", BranchId = salon.BranchId };
        var tableCount = await context.RestaurantTables.CountAsync();
        for (var i = 1; i <= 4; i++)
        {
            garden.Tables.Add(new RestaurantTable { Name = $"Masa {tableCount + i}" });
        }
        context.Areas.Add(garden);
        await context.SaveChangesAsync();
    }

    private static async Task SeedShowcaseAsync(ApplicationDbContext context)
    {
        var items = await context.MenuItems.Where(m => DemoImages.MenuItems.Keys.Contains(m.Name)).ToListAsync();
        var hasFeatured = await context.MenuItems.AnyAsync(m => m.IsFeaturedHome);
        var featured = DemoImages.FeaturedMenuItems.ToList();
        foreach (var item in items)
        {
            item.ImageUrl ??= DemoImages.MenuItems[item.Name];
            var featuredIndex = featured.IndexOf(item.Name);
            if (!hasFeatured && featuredIndex >= 0)
            {
                item.IsFeaturedHome = true;
                item.FeaturedOrder = featuredIndex + 1;
            }
        }

        if (!await context.GalleryImages.AnyAsync())
        {
            context.GalleryImages.AddRange(DemoImages.Gallery.Select((g, i) => new GalleryImage
            {
                ImageUrl = g.Url, Caption = g.Caption, DisplayOrder = i + 1, ShowOnHomepage = g.Home
            }));
        }

        if (!await context.HeroSlides.AnyAsync())
        {
            context.HeroSlides.AddRange(DemoImages.HeroSlides.Select((h, i) => new HeroSlide
            {
                Title = h.Title,
                Subtitle = h.Subtitle,
                ImageUrl = h.Url,
                PrimaryButtonText = "Menüyü İncele",
                PrimaryButtonUrl = "/menu",
                SecondaryButtonText = "Rezervasyon Yap",
                SecondaryButtonUrl = "/rezervasyon",
                DisplayOrder = i + 1,
                IsActive = true
            }));
        }

        await context.SaveChangesAsync();
    }
}
