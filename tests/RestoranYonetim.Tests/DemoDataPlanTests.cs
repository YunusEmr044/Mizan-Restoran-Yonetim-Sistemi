using RestoranYonetim.Data;
using RestoranYonetim.Models;
using RestoranYonetim.Services;

namespace RestoranYonetim.Tests;

public class DemoDataPlanTests
{
    private static readonly DateTime Now = new(2026, 11, 2, 20, 15, 0);
    private static readonly DateTime Today = Now.Date;

    private static List<MenuItem> Menu()
    {
        var categories = new[] { "Kahvaltı", "Çorbalar", "Kebap ve Izgara", "Pide ve Lahmacun", "Döner ve Dürüm", "Salatalar ve Mezeler", "Tatlılar", "İçecekler" }
            .Select((name, i) => new Category { Id = i + 1, Name = name }).ToDictionary(c => c.Name);
        var rows = new (string Category, string Name, decimal Price, MenuDepartment Dept)[]
        {
            ("Kahvaltı", "Serpme Kahvaltı", 750, MenuDepartment.Mutfak), ("Kahvaltı", "Menemen", 200, MenuDepartment.Mutfak),
            ("Çorbalar", "Mercimek Çorbası", 120, MenuDepartment.Mutfak), ("Çorbalar", "Ezogelin Çorbası", 120, MenuDepartment.Mutfak),
            ("Kebap ve Izgara", "Adana Kebap", 380, MenuDepartment.Mutfak), ("Kebap ve Izgara", "Urfa Kebap", 380, MenuDepartment.Mutfak),
            ("Kebap ve Izgara", "Izgara Köfte", 350, MenuDepartment.Mutfak), ("Kebap ve Izgara", "Tavuk Şiş", 300, MenuDepartment.Mutfak),
            ("Kebap ve Izgara", "Karışık Izgara", 700, MenuDepartment.Mutfak),
            ("Pide ve Lahmacun", "Lahmacun", 120, MenuDepartment.Mutfak), ("Pide ve Lahmacun", "Kıymalı Pide", 270, MenuDepartment.Mutfak),
            ("Döner ve Dürüm", "Et Döner", 300, MenuDepartment.Mutfak), ("Döner ve Dürüm", "Tavuk Döner Dürüm", 220, MenuDepartment.Mutfak),
            ("Salatalar ve Mezeler", "Çoban Salata", 150, MenuDepartment.Mutfak), ("Salatalar ve Mezeler", "Humus", 140, MenuDepartment.Mutfak),
            ("Tatlılar", "Künefe", 250, MenuDepartment.Mutfak), ("Tatlılar", "Fıstıklı Baklava", 220, MenuDepartment.Mutfak),
            ("İçecekler", "Ayran", 70, MenuDepartment.Bar), ("İçecekler", "Kola", 90, MenuDepartment.Bar),
            ("İçecekler", "Türk Çayı", 40, MenuDepartment.Bar), ("İçecekler", "Türk Kahvesi", 90, MenuDepartment.Bar),
            ("İçecekler", "Şalgam", 80, MenuDepartment.Bar)
        };

        return rows.Select((r, i) => new MenuItem
        {
            Id = 100 + i, Name = r.Name, Price = r.Price, Department = r.Dept,
            CategoryId = categories[r.Category].Id, Category = categories[r.Category]
        }).ToList();
    }

    private static List<RestaurantTable> Tables() =>
        Enumerable.Range(1, 10).Select(i => new RestaurantTable { Id = i, Name = $"Masa {i}", AreaId = i <= 6 ? 1 : 2 }).ToList();

    private static DemoData Build() => DemoDataPlan.Build(Now, Menu(), Tables());

    private static List<Adisyon> Closed(DemoData d) => d.Adisyonlar.Where(a => a.Status == AdisyonStatus.Odendi).ToList();

    [Fact]
    public void Build_IsDeterministic()
    {
        var a = Build();
        var b = Build();

        Assert.Equal(a.Adisyonlar.Count, b.Adisyonlar.Count);
        Assert.Equal(a.Adisyonlar.SelectMany(x => x.Payments).Sum(p => p.Amount), b.Adisyonlar.SelectMany(x => x.Payments).Sum(p => p.Amount));
        Assert.Equal(a.Customers.Select(c => c.Name), b.Customers.Select(c => c.Name));
    }

    [Fact]
    public void Build_DatesInRange()
    {
        var d = Build();

        Assert.All(Closed(d), a =>
        {
            Assert.InRange(a.ClosedAt!.Value, Today.AddDays(-30), Now);
            Assert.True(a.CreatedAt < a.ClosedAt);
        });
        Assert.Contains(Closed(d), a => a.ClosedAt!.Value.Date == Today);
        Assert.All(d.Adisyonlar.Where(a => a.Status != AdisyonStatus.Odendi), a => Assert.InRange(a.CreatedAt, Today, Now));
        Assert.All(d.Reservations, r => Assert.InRange(r.Date, Today.AddDays(-30), Today.AddDays(7)));
    }

    [Fact]
    public void Build_ClosedAdisyonPaymentsEqualTotal()
    {
        var closed = Closed(Build());

        Assert.NotEmpty(closed);
        Assert.All(closed, a =>
        {
            Assert.True(a.GrandTotal > 0);
            Assert.Equal(a.GrandTotal, a.PaidTotal);
            Assert.All(a.Orders, o => Assert.False(o.IsDraft));
        });
    }

    [Fact]
    public void Build_DailyVolume()
    {
        var perDay = Closed(Build()).Where(a => a.ClosedAt < Today).GroupBy(a => a.ClosedAt!.Value.Date).ToDictionary(g => g.Key, g => g.Count());

        Assert.Equal(30, perDay.Count);
        Assert.All(perDay.Values, n => Assert.InRange(n, 12, 56));
        var weekend = perDay.Where(p => p.Key.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Saturday).Average(p => p.Value);
        var monday = perDay.Where(p => p.Key.DayOfWeek == DayOfWeek.Monday).Average(p => p.Value);
        Assert.True(weekend > monday, $"hafta sonu {weekend} / pazartesi {monday}");
    }

    [Fact]
    public void Build_FoodLeadsTopSellers()
    {
        var menu = Menu().ToDictionary(m => m.Id);
        var top = Closed(Build()).SelectMany(a => a.Orders).SelectMany(o => o.Items)
            .GroupBy(i => i.MenuItemId).OrderByDescending(g => g.Sum(i => i.Quantity)).Take(5)
            .Select(g => menu[g.Key]).ToList();

        Assert.True(top.Count(m => m.Department == MenuDepartment.Mutfak) >= 3, string.Join(", ", top.Select(m => m.Name)));
        Assert.Contains(top, m => m.Category!.Name == "Kebap ve Izgara");
    }

    [Fact]
    public void Build_ShiftCountsMatchCash()
    {
        var d = Build();

        var open = Assert.Single(d.Shifts, s => s.ClosedAt == null);
        Assert.NotEmpty(open.Payments);
        Assert.All(open.Payments, p => Assert.InRange(p.CreatedAt, open.OpenedAt, Now));
        Assert.All(d.Shifts.Where(s => s.ClosedAt != null), s =>
            Assert.InRange(s.ClosingCountedAmount!.Value - (s.OpeningAmount + s.CashPaymentsTotal), -20m, 20m));
        Assert.All(Closed(d).SelectMany(a => a.Payments), p => Assert.Contains(d.Shifts, s => s.Payments.Contains(p)));
    }

    [Fact]
    public void Build_InventoryConsistent()
    {
        var d = Build();

        Assert.Equal(15, d.Inventory.Count);
        Assert.All(d.Inventory, item =>
        {
            Assert.Equal(item.CurrentQuantity, d.InventoryTransactions.Where(t => t.InventoryItem == item).Sum(t => t.QuantityChange));
            Assert.True(item.CurrentQuantity >= 0, item.Name);
        });
        Assert.Equal(2, d.Inventory.Count(i => i.CurrentQuantity < i.MinLevel));
        Assert.Equal(10, d.Recipes.Select(r => r.MenuItemId).Distinct().Count());
        Assert.Equal(2, d.Purchases.Count);
        Assert.Equal(3, d.Suppliers.Count);
    }

    [Fact]
    public void Build_OpenAdisyonsUseDistinctTables()
    {
        var tables = Tables().Take(3).ToList();
        var open = DemoDataPlan.Build(Now, Menu(), tables).Adisyonlar.Where(a => a.Status != AdisyonStatus.Odendi).ToList();

        Assert.Equal(3, open.Select(a => a.TableId).Distinct().Count());
    }

    [Fact]
    public void Build_TodayState()
    {
        var d = Build();
        var menu = Menu().ToDictionary(m => m.Id);
        var open = d.Adisyonlar.Where(a => a.Status != AdisyonStatus.Odendi).ToList();
        var openItems = open.SelectMany(a => a.Orders).SelectMany(o => o.Items).ToList();

        Assert.Equal(3, open.Count);
        Assert.Contains(openItems, i => i.Status == OrderStatus.Hazirlaniyor && menu[i.MenuItemId].Department == MenuDepartment.Mutfak);
        Assert.Contains(openItems, i => i.Status == OrderStatus.Yeni && menu[i.MenuItemId].Department == MenuDepartment.Bar);
        Assert.Single(d.DiscountRequests, r => r.Status == DiscountRequestStatus.Beklemede);
        Assert.Single(d.WaiterCalls, w => !w.Resolved);
        Assert.All(open, a => Assert.NotEqual(TableStatus.Bos, d.TableStatuses[a.TableId!.Value]));
        Assert.All(open.SelectMany(a => a.Orders), o => Assert.InRange(o.SentAt!.Value, Today, Now));
    }

    [Fact]
    public void Build_Counts()
    {
        var d = Build();

        Assert.Equal(25, d.Customers.Count);
        Assert.All(d.Customers, c =>
        {
            Assert.Equal(CustomerService.LevelFor(c.TotalSpent), c.Level);
            Assert.Equal(10, c.Phone.Length);
        });
        Assert.Equal(4, d.Customers.Select(c => c.Level).Distinct().Count());
        Assert.Equal(25, d.Customers.Select(c => c.Phone).Distinct().Count());

        Assert.Equal(12, d.Feedbacks.Count);
        Assert.Equal(6, d.Feedbacks.Count(f => f.IsPublished));
        Assert.Equal(new int?[] { 1, 2, 3, 4 }, d.Feedbacks.Where(f => f.HomeDisplayOrder != null).Select(f => f.HomeDisplayOrder).OrderBy(x => x));
        Assert.All(d.Feedbacks.Where(f => f.HomeDisplayOrder != null), f => Assert.True(f.IsPublished));

        Assert.Equal(13, d.Reservations.Count);
        Assert.Equal(8, d.Reservations.Count(r => r.Date > Today));
        Assert.Single(d.Reservations, r => r.Date == Today && r.Status == ReservationStatus.Onaylandi);
        Assert.Single(d.Campaigns);
        Assert.Equal(2, d.ChefSpecials.Count);
    }
}
