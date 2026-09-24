using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RestoranYonetim.Models;
using RestoranYonetim.Security;

namespace RestoranYonetim.Data;

// GÜVENLİK: sabit demo hesaplar (admin + 9 rol hesabı) SADECE Development ortamında oluşturulur;
// Production'da ilk SuperAdmin SeedInitialProductionAdminAsync ile ortam değişkenlerinden bir kereliğine kurulur.
public static class DbSeeder
{
    public const string AdminEmail = "admin@restoran.local";
    public const string AdminPassword = "Admin123!";

    public static async Task SeedAsync(IServiceProvider services)
    {
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("DbSeeder");
        var context = services.GetRequiredService<ApplicationDbContext>();
        var env = services.GetRequiredService<IHostEnvironment>();

        try
        {
            await context.Database.MigrateAsync();
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Veritabanı migration'ları uygulanamadı - uygulama eski/eksik bir şemayla devam ediyor olabilir.");
        }

        // Her seed adımı kendi try/catch'i ile korunur: tablo/kolon eksikse o adım atlanır, diğerleri yine de çalışır.
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        try
        {
            foreach (var role in RolePermissions.AllRoles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Roller oluşturulurken hata oluştu.");
        }

        try
        {
            await SeedFeatureSettingsAsync(context);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "FeatureSettings seed edilemedi (tablo eksik olabilir). " +
                "Veritabanının yeniden oluşturulması gerekebilir.");
        }

        try
        {
            await SeedRolePermissionsAsync(context);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "RolePermissions seed edilemedi (tablo eksik olabilir). " +
                "Veritabanının yeniden oluşturulması gerekebilir.");
        }

        // ÖNEMLİ: HomeSection/WorkingHoursDay YAPISAL veridir (demo içerik değil), her ortamda seed edilir.
        try
        {
            await SeedHomeSectionsAsync(context);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "HomeSections seed edilemedi (tablo eksik olabilir). " +
                "Veritabanının yeniden oluşturulması gerekebilir.");
        }

        try
        {
            await SeedWorkingHoursAsync(context);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "WorkingHoursDay seed edilemedi (tablo eksik olabilir). " +
                "Veritabanının yeniden oluşturulması gerekebilir.");
        }

        try
        {
            await SeedHomeFeaturesAsync(context);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "HomeFeatures seed edilemedi (tablo eksik olabilir). " +
                "Veritabanının yeniden oluşturulması gerekebilir.");
        }

        try
        {
            await SeedHomeStatsAsync(context);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "HomeStats seed edilemedi (tablo eksik olabilir). " +
                "Veritabanının yeniden oluşturulması gerekebilir.");
        }

        try
        {
            await SeedPageSeoAsync(context);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "PageSeo seed edilemedi (tablo eksik olabilir). " +
                "Veritabanının yeniden oluşturulması gerekebilir.");
        }

        try
        {
            await SeedFaqAsync(context);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Faq seed edilemedi (tablo eksik olabilir). " +
                "Veritabanının yeniden oluşturulması gerekebilir.");
        }

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        if (!env.IsDevelopment())
        {
            try
            {
                await SeedInitialProductionAdminAsync(userManager, logger);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Production ilk SuperAdmin kurulumu sırasında hata oluştu.");
            }

            return;
        }

        // Aşağıdaki adımlar SADECE Development ortamında çalışır.
        try
        {
            var adminUser = await userManager.FindByEmailAsync(AdminEmail);
            if (adminUser == null)
            {
                adminUser = new ApplicationUser
                {
                    UserName = AdminEmail,
                    Email = AdminEmail,
                    FullName = "Süper Admin",
                    EmailConfirmed = true
                };

                var result = await userManager.CreateAsync(adminUser, AdminPassword);
                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(adminUser, RolePermissions.SuperAdmin);
                }
                else
                {
                    logger.LogError("Süper Admin kullanıcısı oluşturulamadı: {Errors}",
                        string.Join("; ", result.Errors.Select(e => e.Description)));
                }
            }
            else if (!await userManager.IsInRoleAsync(adminUser, RolePermissions.SuperAdmin))
            {
                await userManager.AddToRoleAsync(adminUser, RolePermissions.SuperAdmin);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Süper Admin kullanıcısı oluşturulurken/doğrulanırken hata oluştu.");
        }

        try
        {
            await SeedRoleUsersAsync(userManager);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Örnek rol kullanıcıları oluşturulurken hata oluştu.");
        }

        try
        {
            await SeedMenuCatalogAsync(context);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Menü kataloğu seed edilemedi (kolon/tablo eksik olabilir, örn. Department). " +
                "Veritabanının yeniden oluşturulması gerekebilir.");
        }

        try
        {
            await SeedSiteContentAsync(context);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Site içeriği seed edilemedi (kolon/tablo eksik olabilir). " +
                "Veritabanının yeniden oluşturulması gerekebilir.");
        }

        try
        {
            if (!context.Restaurants.Any())
            {
                var restoran = new Restaurant { Name = "Örnek Restoran" };
                context.Restaurants.Add(restoran);
                await context.SaveChangesAsync();

                var sube = new Branch { Name = "Merkez Şube", RestaurantId = restoran.Id };
                context.Branches.Add(sube);
                await context.SaveChangesAsync();

                var alan = new Area { Name = "Salon", BranchId = sube.Id };
                context.Areas.Add(alan);
                await context.SaveChangesAsync();

                for (var i = 1; i <= 6; i++)
                {
                    context.RestaurantTables.Add(new RestaurantTable
                    {
                        Name = $"Masa {i}",
                        QrToken = Guid.NewGuid().ToString("N"),
                        AreaId = alan.Id
                    });
                }
                await context.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Örnek restoran/şube/masa seed edilemedi.");
        }
    }

    private static async Task SeedMenuCatalogAsync(ApplicationDbContext context)
    {
        var categories = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var categoryNames = new[] { "Kahvaltı", "Çorbalar", "Kebap ve Izgara", "Ana Yemekler", "Pide ve Lahmacun", "Döner ve Dürüm", "Ara Sıcaklar ve Atıştırmalık", "Salatalar ve Mezeler", "Tatlılar", "İçecekler" };

        for (var i = 0; i < categoryNames.Length; i++)
        {
            var category = await context.Categories.FirstOrDefaultAsync(c => c.Name == categoryNames[i]);
            if (category == null)
            {
                category = new Category { Name = categoryNames[i], DisplayOrder = i + 1 };
                context.Categories.Add(category);
                await context.SaveChangesAsync();
            }
            else if (category.DisplayOrder != i + 1)
            {
                category.DisplayOrder = i + 1;
                await context.SaveChangesAsync();
            }

            categories[category.Name] = category.Id;
        }

        var items = new[]
        {
            ("Kahvaltı", "Serpme Kahvaltı", "2 kişilik", 750m, MenuDepartment.Mutfak), ("Kahvaltı", "Van Kahvaltısı", "2 kişilik", 850m, MenuDepartment.Mutfak), ("Kahvaltı", "Köy Kahvaltısı", "1 kişilik", 350m, MenuDepartment.Mutfak), ("Kahvaltı", "Kahvaltı Tabağı", "1 kişilik", 300m, MenuDepartment.Mutfak), ("Kahvaltı", "Sucuklu Yumurta", "150 g", 220m, MenuDepartment.Mutfak), ("Kahvaltı", "Menemen", "250 g", 200m, MenuDepartment.Mutfak), ("Kahvaltı", "Kaşarlı Menemen", "300 g", 230m, MenuDepartment.Mutfak), ("Kahvaltı", "Kavurmalı Yumurta", "180 g", 280m, MenuDepartment.Mutfak), ("Kahvaltı", "Sade Omlet", "2 yumurta", 170m, MenuDepartment.Mutfak), ("Kahvaltı", "Kaşarlı Omlet", "250 g", 210m, MenuDepartment.Mutfak), ("Kahvaltı", "Sucuklu Omlet", "250 g", 230m, MenuDepartment.Mutfak), ("Kahvaltı", "Karışık Omlet", "300 g", 250m, MenuDepartment.Mutfak),
            ("Çorbalar", "Mercimek Çorbası", "300 ml", 120m, MenuDepartment.Mutfak), ("Çorbalar", "Ezogelin Çorbası", "300 ml", 120m, MenuDepartment.Mutfak), ("Çorbalar", "Tavuk Çorbası", "300 ml", 140m, MenuDepartment.Mutfak), ("Çorbalar", "Yayla Çorbası", "300 ml", 120m, MenuDepartment.Mutfak), ("Çorbalar", "İşkembe Çorbası", "300 ml", 180m, MenuDepartment.Mutfak), ("Çorbalar", "Kelle Paça", "300 ml", 220m, MenuDepartment.Mutfak), ("Çorbalar", "Beyran", "300 ml", 250m, MenuDepartment.Mutfak), ("Çorbalar", "Domates Çorbası", "300 ml", 130m, MenuDepartment.Mutfak),
            ("Kebap ve Izgara", "Adana Kebap", "180 g", 380m, MenuDepartment.Mutfak), ("Kebap ve Izgara", "Urfa Kebap", "180 g", 380m, MenuDepartment.Mutfak), ("Kebap ve Izgara", "Patlıcan Kebap", "180 g", 420m, MenuDepartment.Mutfak), ("Kebap ve Izgara", "Beyti Kebap", "200 g", 450m, MenuDepartment.Mutfak), ("Kebap ve Izgara", "Alinazik", "180 g", 450m, MenuDepartment.Mutfak), ("Kebap ve Izgara", "Karışık Kebap", "350 g", 650m, MenuDepartment.Mutfak), ("Kebap ve Izgara", "Izgara Köfte", "200 g", 350m, MenuDepartment.Mutfak), ("Kebap ve Izgara", "Kaşarlı Köfte", "200 g", 390m, MenuDepartment.Mutfak), ("Kebap ve Izgara", "Tavuk Şiş", "200 g", 300m, MenuDepartment.Mutfak), ("Kebap ve Izgara", "Tavuk Kanat", "250 g", 320m, MenuDepartment.Mutfak), ("Kebap ve Izgara", "Tavuk Pirzola", "250 g", 330m, MenuDepartment.Mutfak), ("Kebap ve Izgara", "Izgara Pirzola", "250 g", 550m, MenuDepartment.Mutfak), ("Kebap ve Izgara", "Kuzu Şiş", "200 g", 500m, MenuDepartment.Mutfak), ("Kebap ve Izgara", "Kuzu Pirzola", "250 g", 600m, MenuDepartment.Mutfak), ("Kebap ve Izgara", "Dana Antrikot", "300 g", 650m, MenuDepartment.Mutfak), ("Kebap ve Izgara", "Karışık Izgara", "400 g", 700m, MenuDepartment.Mutfak),
            ("Ana Yemekler", "Etli Kuru Fasulye", "300 g", 250m, MenuDepartment.Mutfak), ("Ana Yemekler", "Etli Nohut", "300 g", 250m, MenuDepartment.Mutfak), ("Ana Yemekler", "Etli Taze Fasulye", "300 g", 270m, MenuDepartment.Mutfak), ("Ana Yemekler", "Tas Kebabı", "300 g", 350m, MenuDepartment.Mutfak), ("Ana Yemekler", "Hünkar Beğendi", "300 g", 400m, MenuDepartment.Mutfak), ("Ana Yemekler", "Karnıyarık", "1 adet / 300 g", 300m, MenuDepartment.Mutfak), ("Ana Yemekler", "İmam Bayıldı", "1 adet / 280 g", 270m, MenuDepartment.Mutfak), ("Ana Yemekler", "Etli Türlü", "300 g", 320m, MenuDepartment.Mutfak), ("Ana Yemekler", "Sac Kavurma", "250 g", 420m, MenuDepartment.Mutfak), ("Ana Yemekler", "Et Sote", "250 g", 400m, MenuDepartment.Mutfak), ("Ana Yemekler", "Tavuk Sote", "250 g", 300m, MenuDepartment.Mutfak), ("Ana Yemekler", "Güveç", "350 g", 400m, MenuDepartment.Mutfak),
            ("Pide ve Lahmacun", "Lahmacun", "120 g", 120m, MenuDepartment.Mutfak), ("Pide ve Lahmacun", "Acılı Lahmacun", "120 g", 125m, MenuDepartment.Mutfak), ("Pide ve Lahmacun", "Kuşbaşılı Pide", "200 g", 300m, MenuDepartment.Mutfak), ("Pide ve Lahmacun", "Kaşarlı Pide", "200 g", 250m, MenuDepartment.Mutfak), ("Pide ve Lahmacun", "Kıymalı Pide", "200 g", 270m, MenuDepartment.Mutfak), ("Pide ve Lahmacun", "Sucuklu Pide", "200 g", 280m, MenuDepartment.Mutfak), ("Pide ve Lahmacun", "Sucuklu Kaşarlı Pide", "220 g", 320m, MenuDepartment.Mutfak), ("Pide ve Lahmacun", "Kavurmalı Pide", "220 g", 350m, MenuDepartment.Mutfak), ("Pide ve Lahmacun", "Kavurmalı Kaşarlı Pide", "250 g", 380m, MenuDepartment.Mutfak), ("Pide ve Lahmacun", "Karışık Pide", "250 g", 350m, MenuDepartment.Mutfak),
            ("Döner ve Dürüm", "Et Döner", "120 g", 300m, MenuDepartment.Mutfak), ("Döner ve Dürüm", "Tavuk Döner", "120 g", 220m, MenuDepartment.Mutfak), ("Döner ve Dürüm", "İskender", "150 g et", 380m, MenuDepartment.Mutfak), ("Döner ve Dürüm", "Et Döner Dürüm", "120 g et", 300m, MenuDepartment.Mutfak), ("Döner ve Dürüm", "Tavuk Döner Dürüm", "120 g tavuk", 220m, MenuDepartment.Mutfak), ("Döner ve Dürüm", "Et Tantuni", "120 g et", 280m, MenuDepartment.Mutfak), ("Döner ve Dürüm", "Tavuk Tantuni", "120 g tavuk", 220m, MenuDepartment.Mutfak), ("Döner ve Dürüm", "Hamburger", "150 g köfte", 350m, MenuDepartment.Mutfak), ("Döner ve Dürüm", "Cheeseburger", "150 g köfte", 390m, MenuDepartment.Mutfak), ("Döner ve Dürüm", "Tavuk Burger", "150 g tavuk", 300m, MenuDepartment.Mutfak),
            ("Ara Sıcaklar ve Atıştırmalık", "Patates Kızartması", "200 g", 150m, MenuDepartment.Mutfak), ("Ara Sıcaklar ve Atıştırmalık", "Baharatlı Patates", "200 g", 170m, MenuDepartment.Mutfak), ("Ara Sıcaklar ve Atıştırmalık", "Sigara Böreği", "6 adet", 180m, MenuDepartment.Mutfak), ("Ara Sıcaklar ve Atıştırmalık", "Paçanga Böreği", "4 adet", 220m, MenuDepartment.Mutfak), ("Ara Sıcaklar ve Atıştırmalık", "İçli Köfte", "2 adet", 200m, MenuDepartment.Mutfak), ("Ara Sıcaklar ve Atıştırmalık", "Mercimek Köftesi", "200 g", 160m, MenuDepartment.Mutfak), ("Ara Sıcaklar ve Atıştırmalık", "Çıtır Tavuk", "250 g", 280m, MenuDepartment.Mutfak), ("Ara Sıcaklar ve Atıştırmalık", "Soğan Halkası", "200 g", 170m, MenuDepartment.Mutfak), ("Ara Sıcaklar ve Atıştırmalık", "Sıcak Meze Tabağı", "300 g", 300m, MenuDepartment.Mutfak), ("Ara Sıcaklar ve Atıştırmalık", "Karışık Atıştırmalık Tabağı", "400 g", 400m, MenuDepartment.Mutfak),
            ("Salatalar ve Mezeler", "Çoban Salata", "250 g", 150m, MenuDepartment.Mutfak), ("Salatalar ve Mezeler", "Mevsim Salata", "250 g", 150m, MenuDepartment.Mutfak), ("Salatalar ve Mezeler", "Gavurdağı Salatası", "250 g", 180m, MenuDepartment.Mutfak), ("Salatalar ve Mezeler", "Sezar Salata", "300 g", 250m, MenuDepartment.Mutfak), ("Salatalar ve Mezeler", "Akdeniz Salatası", "300 g", 230m, MenuDepartment.Mutfak), ("Salatalar ve Mezeler", "Haydari", "150 g", 130m, MenuDepartment.Mutfak), ("Salatalar ve Mezeler", "Acılı Ezme", "150 g", 130m, MenuDepartment.Mutfak), ("Salatalar ve Mezeler", "Humus", "150 g", 140m, MenuDepartment.Mutfak), ("Salatalar ve Mezeler", "Patlıcan Ezmesi", "150 g", 140m, MenuDepartment.Mutfak), ("Salatalar ve Mezeler", "Karışık Meze Tabağı", "400 g", 350m, MenuDepartment.Mutfak),
            ("Tatlılar", "Künefe", "200 g", 250m, MenuDepartment.Mutfak), ("Tatlılar", "Fıstıklı Baklava", "2 dilim / 120 g", 220m, MenuDepartment.Mutfak), ("Tatlılar", "Sütlaç", "250 g", 150m, MenuDepartment.Mutfak), ("Tatlılar", "Kazandibi", "180 g", 160m, MenuDepartment.Mutfak), ("Tatlılar", "Fırın Sütlaç", "250 g", 180m, MenuDepartment.Mutfak), ("Tatlılar", "Profiterol", "200 g", 200m, MenuDepartment.Mutfak), ("Tatlılar", "Cheesecake", "150 g", 220m, MenuDepartment.Mutfak), ("Tatlılar", "Tiramisu", "150 g", 220m, MenuDepartment.Mutfak), ("Tatlılar", "Dondurma", "3 top / 150 g", 180m, MenuDepartment.Mutfak), ("Tatlılar", "Katmer", "200 g", 250m, MenuDepartment.Mutfak),
            ("İçecekler", "Ayran", "300 ml", 70m, MenuDepartment.Bar), ("İçecekler", "Kola", "330 ml", 90m, MenuDepartment.Bar), ("İçecekler", "Fanta", "330 ml", 90m, MenuDepartment.Bar), ("İçecekler", "Sprite", "330 ml", 90m, MenuDepartment.Bar), ("İçecekler", "Gazoz", "250 ml", 80m, MenuDepartment.Bar), ("İçecekler", "Şalgam", "300 ml", 80m, MenuDepartment.Bar), ("İçecekler", "Soda", "200 ml", 50m, MenuDepartment.Bar), ("İçecekler", "Meyve Suyu", "330 ml", 90m, MenuDepartment.Bar), ("İçecekler", "Portakal Suyu", "300 ml", 150m, MenuDepartment.Bar), ("İçecekler", "Limonata", "300 ml", 130m, MenuDepartment.Bar), ("İçecekler", "Su", "500 ml", 30m, MenuDepartment.Bar), ("İçecekler", "Türk Çayı", "1 bardak", 40m, MenuDepartment.Bar), ("İçecekler", "Türk Kahvesi", "1 fincan", 90m, MenuDepartment.Bar), ("İçecekler", "Filtre Kahve", "250 ml", 130m, MenuDepartment.Bar), ("İçecekler", "Cappuccino", "250 ml", 150m, MenuDepartment.Bar), ("İçecekler", "Latte", "300 ml", 160m, MenuDepartment.Bar), ("İçecekler", "Espresso", "30 ml", 100m, MenuDepartment.Bar)
        };

        foreach (var itemData in items)
        {
            var item = await context.MenuItems.FirstOrDefaultAsync(m => m.Name == itemData.Item2 && m.CategoryId == categories[itemData.Item1]);
            if (item == null)
            {
                context.MenuItems.Add(new MenuItem { Name = itemData.Item2, Description = itemData.Item3, Price = itemData.Item4, CategoryId = categories[itemData.Item1], IsAvailable = true, Department = itemData.Item5 });
            }
            else
            {
                item.Description = itemData.Item3;
                item.Price = itemData.Item4;
                item.IsAvailable = true;
                item.Department = itemData.Item5;
            }
        }

        await context.SaveChangesAsync();
    }

    private static async Task SeedSiteContentAsync(ApplicationDbContext context)
    {
        if (!context.SiteContents.Any())
        {
            context.SiteContents.Add(new SiteContent
            {
                HeroTitle = "Restoranımıza Hoş Geldiniz",
                HeroSubtitle = "Taze malzemeler, güler yüzlü hizmet.",
                HeroPrimaryButtonText = "Menümüzü İnceleyin",
                HeroSecondaryButtonText = "Rezervasyon Yapın",
                AboutTitle = "Hakkımızda",
                AboutText = "Bu metni admin panelindeki \"Site İçeriği\" bölümünden düzenleyebilirsiniz.",
                ContactPhone = "0000 000 00 00",
                ContactAddress = "Adresinizi admin panelinden girin.",
                ReservationEnabled = true
            });
            await context.SaveChangesAsync();
        }
    }

    private static async Task SeedFeatureSettingsAsync(ApplicationDbContext context)
    {
        var existing = await context.FeatureSettings.ToListAsync();
        foreach (var feature in FeatureCatalog.All)
        {
            if (existing.Any(item => item.Key == feature.Key))
            {
                continue;
            }

            context.FeatureSettings.Add(new FeatureSetting
            {
                Key = feature.Key,
                DisplayName = feature.DisplayName,
                Permission = feature.PermissionPrefix,
                IsEnabled = true
            });
        }

        await context.SaveChangesAsync();
    }

    // Add-only: katalogda olup DB'de olmayanı ekler, var olana dokunmaz.
    private static async Task SeedHomeSectionsAsync(ApplicationDbContext context)
    {
        var existing = await context.HomeSections.ToListAsync();
        foreach (var section in HomeSectionCatalog.All)
        {
            if (existing.Any(s => s.Key == section.Key))
            {
                continue;
            }

            context.HomeSections.Add(new HomeSection
            {
                Key = section.Key,
                IsEnabled = true,
                DisplayOrder = section.DefaultOrder
            });
        }

        await context.SaveChangesAsync();
    }

    private static async Task SeedWorkingHoursAsync(ApplicationDbContext context)
    {
        var existing = await context.WorkingHoursDays.ToListAsync();
        foreach (DayOfWeek day in Enum.GetValues(typeof(DayOfWeek)))
        {
            if (existing.Any(d => d.DayOfWeek == day))
            {
                continue;
            }

            context.WorkingHoursDays.Add(new WorkingHoursDay
            {
                DayOfWeek = day,
                IsClosed = false,
                OpenTime = new TimeSpan(10, 0, 0),
                CloseTime = new TimeSpan(22, 0, 0)
            });
        }

        await context.SaveChangesAsync();
    }

    // Add-only: admin bir tanesini bile eklediyse bu metot bir daha hiçbir şey eklemez.
    private static async Task SeedHomeFeaturesAsync(ApplicationDbContext context)
    {
        if (await context.HomeFeatures.AnyAsync())
        {
            return;
        }

        context.HomeFeatures.AddRange(
            new HomeFeature { Title = "Taze Malzemeler", Description = "Her gün özenle seçilen taze malzemelerle hazırlanan lezzetler sizi bekliyor.", Icon = "bi-egg-fried", DisplayOrder = 1, IsActive = true },
            new HomeFeature { Title = "QR ile Kolay Sipariş", Description = "Masanızdaki QR kodu okutun, dilerseniz beklemeden doğrudan sipariş verin.", Icon = "bi-qr-code-scan", DisplayOrder = 2, IsActive = true },
            new HomeFeature { Title = "Sıcak Atmosfer", Description = "Misafirperverliğimizle her ziyaretinizi keyifli bir anıya dönüştürüyoruz.", Icon = "bi-emoji-smile", DisplayOrder = 3, IsActive = true }
        );
        await context.SaveChangesAsync();
    }

    private static async Task SeedHomeStatsAsync(ApplicationDbContext context)
    {
        if (await context.HomeStats.AnyAsync())
        {
            return;
        }

        context.HomeStats.AddRange(
            new HomeStat { Value = "15+", Label = "Yıllık Deneyim", Icon = "bi-award", DisplayOrder = 1, IsActive = true },
            new HomeStat { Value = "50+", Label = "Menü Çeşidi", Icon = "bi-journal-richtext", DisplayOrder = 2, IsActive = true },
            new HomeStat { Value = "1000+", Label = "Mutlu Misafir", Icon = "bi-emoji-heart-eyes", DisplayOrder = 3, IsActive = true },
            new HomeStat { Value = "4.8/5", Label = "Misafir Puanı", Icon = "bi-star-fill", DisplayOrder = 4, IsActive = true }
        );
        await context.SaveChangesAsync();
    }

    // Add-only: eksikse boş bir PageSeo satırı ekler, var olan bir satıra dokunulmaz.
    private static async Task SeedPageSeoAsync(ApplicationDbContext context)
    {
        var existing = await context.PageSeos.Select(p => p.PageKey).ToListAsync();
        foreach (var page in SeoPageCatalog.All)
        {
            if (existing.Contains(page.Key))
            {
                continue;
            }

            context.PageSeos.Add(new PageSeo { PageKey = page.Key });
        }

        await context.SaveChangesAsync();
    }

    // Hiç SSS kaydı yoksa genel birkaç örnek soru-cevap eklenir; admin panelden serbestçe düzenlenebilir.
    private static async Task SeedFaqAsync(ApplicationDbContext context)
    {
        if (await context.Faqs.AnyAsync())
        {
            return;
        }

        context.Faqs.AddRange(
            new Faq { Question = "Rezervasyon nasıl yapabilirim?", Answer = "Web sitemizdeki \"Rezervasyon\" sayfasından tarih, saat ve kişi sayısını belirterek talep oluşturabilirsiniz; talebiniz onaylandığında sizinle iletişime geçilir.", Category = "Rezervasyon", DisplayOrder = 1, IsActive = true },
            new Faq { Question = "Masadaki QR kodu nasıl kullanırım?", Answer = "Masanızda bulunan size özel QR kodu telefonunuzun kamerasıyla okutarak güncel menümüze ulaşabilir, dilerseniz doğrudan masanızdan sipariş oluşturabilirsiniz.", Category = "Sipariş", DisplayOrder = 2, IsActive = true },
            new Faq { Question = "Menüdeki fiyatlara ve ürünlere nasıl ulaşabilirim?", Answer = "Güncel menümüzü, ürün açıklamalarımızı ve fiyatlarımızı web sitemizdeki \"Menü\" sayfasından her zaman inceleyebilirsiniz.", Category = "Menü", DisplayOrder = 3, IsActive = true },
            new Faq { Question = "Çalışma saatleriniz nedir?", Answer = "Güncel çalışma saatlerimizi ve şu anda açık olup olmadığımızı \"İletişim\" sayfamızdan görebilirsiniz.", Category = "Çalışma Saatleri", DisplayOrder = 4, IsActive = true },
            new Faq { Question = "Kampanyalarınızdan nasıl haberdar olabilirim?", Answer = "Güncel kampanyalarımızı web sitemizin ana sayfasından ve sosyal medya hesaplarımızdan takip edebilirsiniz.", Category = "Kampanyalar", DisplayOrder = 5, IsActive = true }
        );
        await context.SaveChangesAsync();
    }

    private static async Task SeedRolePermissionsAsync(ApplicationDbContext context)
    {
        var existing = await context.RolePermissions.ToListAsync();
        foreach (var role in RolePermissions.AllRoles)
        {
            var defaults = RolePermissions.Defaults.GetValueOrDefault(role, []);
            foreach (var permission in Permissions.All)
            {
                if (existing.Any(item => item.RoleName == role && item.Permission == permission))
                {
                    continue;
                }

                context.RolePermissions.Add(new RolePermission
                {
                    RoleName = role,
                    Permission = permission,
                    IsEnabled = defaults.Contains(permission)
                });
            }
        }

        await context.SaveChangesAsync();
    }

    private static async Task SeedRoleUsersAsync(UserManager<ApplicationUser> userManager)
    {
        var sampleUsers = new Dictionary<string, string>
        {
            [RolePermissions.RestoranSahibi] = "restoran-sahibi@restoran.local",
            [RolePermissions.Mudur] = "mudur@restoran.local",
            [RolePermissions.Garson] = "garson@restoran.local",
            [RolePermissions.Kasiyer] = "kasiyer@restoran.local",
            [RolePermissions.MutfakPersoneli] = "mutfak@restoran.local",
            [RolePermissions.BarPersoneli] = "bar@restoran.local",
            [RolePermissions.DepoPersoneli] = "depo@restoran.local",
            [RolePermissions.Muhasebe] = "muhasebe@restoran.local",
            [RolePermissions.RaporGoruntuleyici] = "rapor@restoran.local"
        };

        foreach (var (role, email) in sampleUsers)
        {
            var user = await userManager.FindByEmailAsync(email);
            if (user == null)
            {
                user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    FullName = role,
                    EmailConfirmed = true
                };

                var result = await userManager.CreateAsync(user, "Rol1234!");
                if (!result.Succeeded)
                {
                    continue;
                }
            }

            if (!await userManager.IsInRoleAsync(user, role))
            {
                await userManager.AddToRoleAsync(user, role);
            }
        }
    }

    // GÜVENLİK: Production'da kaynak koda hiçbir gerçek kimlik bilgisi yazılmaz. İlk SuperAdmin hesabı sadece
    // sistemde HİÇ kullanıcı yokken ve ortam değişkeni olarak verilen e-posta/şifre ile bir kereliğine oluşturulur.
    private static async Task SeedInitialProductionAdminAsync(UserManager<ApplicationUser> userManager, ILogger logger)
    {
        if (await userManager.Users.AnyAsync())
        {
            return;
        }

        var email = Environment.GetEnvironmentVariable("INITIAL_SUPERADMIN_EMAIL");
        var password = Environment.GetEnvironmentVariable("INITIAL_SUPERADMIN_PASSWORD");

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            // Uygulamayı çökertmiyoruz (herkese açık site yine de çalışabilsin) ama operatöre net bir uyarı veriyoruz.
            logger.LogCritical(
                "Sistemde hiç kullanıcı yok ve INITIAL_SUPERADMIN_EMAIL / INITIAL_SUPERADMIN_PASSWORD " +
                "ortam değişkenleri ayarlanmamış - yönetim paneline giriş yapacak bir hesap oluşturulamadı. " +
                "İlk SuperAdmin hesabını oluşturmak için bu iki ortam değişkenini ayarlayıp uygulamayı " +
                "yeniden başlatın (bkz. README.md \"Production Kurulumu\").");
            return;
        }

        var admin = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = "Süper Admin",
            EmailConfirmed = true
        };

        var result = await userManager.CreateAsync(admin, password);
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(admin, RolePermissions.SuperAdmin);
            // GÜVENLİK: Şifreyi loglamıyoruz.
            logger.LogInformation("İlk SuperAdmin hesabı oluşturuldu: {Email}. " +
                "INITIAL_SUPERADMIN_EMAIL/INITIAL_SUPERADMIN_PASSWORD ortam değişkenlerini artık " +
                "kaldırabilirsiniz (sistemde kullanıcı olduğu için bir daha kullanılmayacaklar).", email);
        }
        else
        {
            logger.LogError("İlk SuperAdmin hesabı oluşturulamadı: {Errors}",
                string.Join("; ", result.Errors.Select(e => e.Description)));
        }
    }
}
