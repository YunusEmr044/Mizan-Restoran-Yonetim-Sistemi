using RestoranYonetim.Models;
using RestoranYonetim.Services;

namespace RestoranYonetim.Data;

public sealed record DemoData(
    List<Adisyon> Adisyonlar,
    List<CashShift> Shifts,
    List<Customer> Customers,
    List<Reservation> Reservations,
    List<Feedback> Feedbacks,
    List<Supplier> Suppliers,
    List<InventoryItem> Inventory,
    List<RecipeItem> Recipes,
    List<Purchase> Purchases,
    List<InventoryTransaction> InventoryTransactions,
    List<Campaign> Campaigns,
    List<ChefSpecial> ChefSpecials,
    List<DiscountRequest> DiscountRequests,
    List<WaiterCall> WaiterCalls,
    Dictionary<int, TableStatus> TableStatuses);

// Geliştirme ortamı için belirlenimci demo işletme verisi üretir; veritabanına dokunmaz.
// Menü ve masalar Id ile referanslanır, böylece EF onları yeniden eklemeye çalışmaz.
public static class DemoDataPlan
{
    private const int Seed = 2026;
    private const int PastDays = 30;
    private const decimal OpeningCash = 1000m;

    private static readonly Dictionary<string, int> CategoryWeights = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Kebap ve Izgara"] = 4, ["Döner ve Dürüm"] = 4, ["Pide ve Lahmacun"] = 3
    };

    private static readonly Dictionary<string, int> ItemWeights = new() { ["Türk Çayı"] = 4, ["Ayran"] = 3 };

    private static readonly string[] CustomerNames =
    {
        "Ayşe Yılmaz", "Mehmet Kaya", "Zeynep Demir", "Mustafa Çelik", "Elif Şahin", "Ahmet Yıldız", "Fatma Aydın",
        "Emre Öztürk", "Selin Arslan", "Burak Doğan", "Deniz Kılıç", "Cem Aslan", "Merve Çetin", "Oğuz Kara",
        "Gizem Koç", "Kerem Kurt", "Ece Özdemir", "Serkan Polat", "Derya Şimşek", "Hakan Erdoğan", "Buse Aksoy",
        "Tolga Güneş", "Nazlı Tekin", "Murat Bulut", "İrem Korkmaz"
    };

    private static readonly (string Name, string Unit, decimal UnitCost, decimal MinLevel, int Supplier)[] StockItems =
    {
        ("Dana Kıyma", "kg", 520m, 15m, 0), ("Kuzu Eti", "kg", 680m, 8m, 0), ("Tavuk But", "kg", 210m, 10m, 0),
        ("Dana Döner", "kg", 560m, 10m, 0), ("Lavaş", "adet", 6m, 200m, 2), ("Pide Hamuru", "adet", 9m, 120m, 2),
        ("Domates", "kg", 35m, 20m, 1), ("Soğan", "kg", 18m, 15m, 1), ("Sivri Biber", "kg", 60m, 8m, 1),
        ("Kırmızı Mercimek", "kg", 75m, 10m, 2), ("Süt", "lt", 32m, 10m, 2), ("Yoğurt", "kg", 70m, 10m, 2),
        ("Türk Kahvesi", "kg", 620m, 2m, 2), ("Siyah Çay", "kg", 340m, 5m, 2), ("Kaşar Peyniri", "kg", 380m, 5m, 2)
    };

    private static readonly string[] CriticalStock = { "Lavaş", "Siyah Çay" };

    private static readonly (string MenuItem, (string Stock, decimal Quantity)[] Lines)[] RecipeDefinitions =
    {
        ("Adana Kebap", new[] { ("Dana Kıyma", 0.18m), ("Lavaş", 1m), ("Domates", 0.05m), ("Sivri Biber", 0.03m) }),
        ("Urfa Kebap", new[] { ("Dana Kıyma", 0.18m), ("Lavaş", 1m), ("Soğan", 0.05m) }),
        ("Izgara Köfte", new[] { ("Dana Kıyma", 0.2m), ("Soğan", 0.03m) }),
        ("Tavuk Şiş", new[] { ("Tavuk But", 0.2m), ("Sivri Biber", 0.03m) }),
        ("Lahmacun", new[] { ("Dana Kıyma", 0.05m), ("Pide Hamuru", 1m), ("Domates", 0.03m) }),
        ("Kıymalı Pide", new[] { ("Dana Kıyma", 0.12m), ("Pide Hamuru", 1m) }),
        ("Et Döner", new[] { ("Dana Döner", 0.12m), ("Lavaş", 1m) }),
        ("Mercimek Çorbası", new[] { ("Kırmızı Mercimek", 0.06m), ("Soğan", 0.02m) }),
        ("Türk Çayı", new[] { ("Siyah Çay", 0.005m) }),
        ("Türk Kahvesi", new[] { ("Türk Kahvesi", 0.007m) })
    };

    private static readonly (string? Name, int Rating, string Comment)[] FeedbackTexts =
    {
        ("Selin A.", 5, "Adana kebap tam kıvamındaydı, ocakbaşında pişmesini izlemek ayrı keyif."),
        ("Burak D.", 5, "Mezeler taze, servis hızlı. Bahçe masaları akşam çok güzel oluyor."),
        ("Gizem K.", 4, "Lahmacunu ince ve çıtır, mercimek çorbası ev yapımı gibi."),
        ("Hakan E.", 5, "Ailece geldik, çocuklar için de ilgili davrandılar. Künefe harikaydı."),
        ("Merve Ç.", 4, "Karışık ızgara iki kişiye fazlasıyla yetti, fiyatı makul."),
        ("Oğuz K.", 5, "QR menüden sipariş verdik, siparişler çok çabuk geldi."),
        ("Ece Ö.", 4, "Kahvaltı tabağı doyurucu, çay hiç bitmedi."),
        (null, 3, "Cumartesi akşamı biraz kalabalıktı, beklemek gerekti."),
        ("Tolga G.", 5, "İskender ve şalgam için yeniden geleceğim."),
        ("Nazlı T.", 4, "Personel çok güler yüzlü, mekân temiz."),
        (null, 3, "Yemekler güzel ama müzik biraz yüksekti."),
        ("Cem A.", 5, "Kadıköy'de ocakbaşı denince ilk aklıma gelen yer oldu.")
    };

    public static DemoData Build(DateTime now, IReadOnlyList<MenuItem> menu, IReadOnlyList<RestaurantTable> tables)
    {
        var rng = new Random(Seed);
        var today = now.Date;
        var pool = WeightedPool(menu);

        var adisyonlar = new List<Adisyon>();
        var shifts = new List<CashShift>();
        for (var dayOffset = PastDays; dayOffset >= 0; dayOffset--)
        {
            var day = today.AddDays(-dayOffset);
            var isToday = dayOffset == 0;
            var shift = new CashShift
            {
                OpenedAt = isToday && now < day.AddHours(12) ? Clamp(today, now.AddHours(-3)) : day.AddHours(11.5),
                ClosedAt = isToday ? null : day.AddHours(23).AddMinutes(59),
                OpeningAmount = OpeningCash,
                OpenedByUserName = "Kasiyer",
                ClosedByUserName = isToday ? null : "Kasiyer"
            };

            var count = (int)Math.Round((22 + rng.Next(0, 11)) * DayMultiplier(day.DayOfWeek));
            for (var i = 0; i < count; i++)
            {
                var adisyon = ClosedAdisyon(rng, day, tables, pool);
                var payments = Payments(rng, adisyon).ToList();
                if (isToday && adisyon.ClosedAt > now.AddMinutes(-5)) continue;
                foreach (var payment in payments)
                {
                    adisyon.Payments.Add(payment);
                    shift.Payments.Add(payment);
                }
                adisyonlar.Add(adisyon);
            }

            if (!isToday)
            {
                var difference = rng.NextDouble() < 0.2 ? rng.Next(-20, 21) : 0;
                shift.ClosingCountedAmount = shift.OpeningAmount + shift.CashPaymentsTotal + difference;
            }
            shifts.Add(shift);
        }

        var (open, discountRequests, waiterCalls, tableStatuses) = TodayState(now, menu, tables);
        adisyonlar.AddRange(open);

        var suppliers = Suppliers();
        var (inventory, recipes, purchases, transactions) = Stock(rng, today, menu, adisyonlar, suppliers);

        return new DemoData(
            adisyonlar, shifts, Customers(rng, today), Reservations(rng, today), Feedbacks(today, tables),
            suppliers, inventory, recipes, purchases, transactions,
            new List<Campaign> { Campaign(today) }, ChefSpecials(today, menu),
            discountRequests, waiterCalls, tableStatuses);
    }

    private static double DayMultiplier(DayOfWeek day) => day switch
    {
        DayOfWeek.Friday or DayOfWeek.Saturday => 1.4,
        DayOfWeek.Monday => 0.8,
        _ => 1.0
    };

    private static DateTime Clamp(DateTime min, DateTime value) => value < min ? min : value;

    private static List<MenuItem> WeightedPool(IReadOnlyList<MenuItem> menu)
    {
        var pool = new List<MenuItem>();
        foreach (var item in menu)
        {
            var weight = ItemWeights.GetValueOrDefault(item.Name, CategoryWeights.GetValueOrDefault(item.Category?.Name ?? string.Empty, 1));
            for (var i = 0; i < weight; i++) pool.Add(item);
        }

        return pool;
    }

    private static Adisyon ClosedAdisyon(Random rng, DateTime day, IReadOnlyList<RestaurantTable> tables, List<MenuItem> pool)
    {
        var roll = rng.NextDouble();
        var created = roll < 0.45 ? day.AddHours(12).AddMinutes(rng.Next(0, 180))
            : roll < 0.55 ? day.AddHours(15).AddMinutes(rng.Next(0, 180))
            : day.AddHours(18).AddMinutes(rng.Next(0, 270));
        var closed = created.AddMinutes(rng.Next(35, 96));
        var lastClose = day.AddHours(23).AddMinutes(55);
        if (closed > lastClose) closed = lastClose;

        var adisyon = new Adisyon
        {
            TableId = tables[rng.Next(tables.Count)].Id,
            Status = AdisyonStatus.Odendi,
            CreatedAt = created,
            ClosedAt = closed
        };

        var orderCount = rng.Next(1, 4);
        var span = (closed - created).TotalMinutes;
        for (var o = 0; o < orderCount; o++)
        {
            var sentAt = created.AddMinutes(span * o / (orderCount + 1) + 1);
            var fromWaiter = rng.NextDouble() < 0.7;
            var order = new Order
            {
                TableId = adisyon.TableId,
                IsDraft = false,
                Source = fromWaiter ? OrderSource.Garson : OrderSource.Musteri,
                Status = OrderStatus.ServisEdildi,
                CreatedAt = sentAt,
                SentAt = sentAt,
                CreatedByUserName = fromWaiter ? "Garson" : null
            };

            foreach (var item in Pick(rng, pool, rng.Next(1, 6)))
            {
                order.Items.Add(Line(item, rng.Next(1, 3), OrderStatus.ServisEdildi));
            }
            adisyon.Orders.Add(order);
        }

        if (rng.NextDouble() < 0.05)
        {
            adisyon.DiscountAmount = Math.Round(adisyon.ItemsTotal * 0.10m, 0);
            adisyon.DiscountReason = "Sadık müşteri indirimi";
        }

        return adisyon;
    }

    private static IEnumerable<MenuItem> Pick(Random rng, List<MenuItem> pool, int count)
    {
        var picked = new List<MenuItem>();
        for (var attempt = 0; picked.Count < count && attempt < count * 4; attempt++)
        {
            var item = pool[rng.Next(pool.Count)];
            if (!picked.Contains(item)) picked.Add(item);
        }

        return picked;
    }

    private static OrderItem Line(MenuItem item, int quantity, OrderStatus status) => new()
    {
        MenuItemId = item.Id,
        MenuItemName = item.Name,
        UnitPrice = item.Price,
        Quantity = quantity,
        Status = status
    };

    private static IEnumerable<Payment> Payments(Random rng, Adisyon adisyon)
    {
        var total = adisyon.GrandTotal;
        var at = adisyon.ClosedAt!.Value;
        var card = rng.NextDouble() < 0.7 ? PaymentMethod.KrediKarti : PaymentMethod.BankaKarti;
        var roll = rng.NextDouble();

        if (roll < 0.05 && total >= 100)
        {
            var cash = Math.Round(total * 0.4m, 0);
            yield return new Payment { Method = PaymentMethod.Nakit, Amount = cash, CreatedAt = at, ReceivedByUserName = "Kasiyer" };
            yield return new Payment { Method = card, Amount = total - cash, CreatedAt = at, ReceivedByUserName = "Kasiyer" };
        }
        else
        {
            yield return new Payment { Method = roll < 0.45 ? PaymentMethod.Nakit : card, Amount = total, CreatedAt = at, ReceivedByUserName = "Kasiyer" };
        }
    }

    private static (List<Adisyon>, List<DiscountRequest>, List<WaiterCall>, Dictionary<int, TableStatus>) TodayState(
        DateTime now, IReadOnlyList<MenuItem> menu, IReadOnlyList<RestaurantTable> tables)
    {
        var today = now.Date;
        DateTime Ago(int minutes) => Clamp(today, now.AddMinutes(-minutes));
        MenuItem Find(string name, MenuDepartment fallback) =>
            menu.FirstOrDefault(m => m.Name == name) ?? menu.First(m => m.Department == fallback);
        Order NewOrder(int tableId, int minutesAgo, OrderStatus status, params (string Name, MenuDepartment Dept, int Qty)[] items)
        {
            var order = new Order
            {
                TableId = tableId, IsDraft = false, Source = OrderSource.Garson, Status = status,
                CreatedAt = Ago(minutesAgo), SentAt = Ago(minutesAgo), CreatedByUserName = "Garson"
            };
            foreach (var (name, dept, qty) in items) order.Items.Add(Line(Find(name, dept), qty, status));
            return order;
        }

        var kitchenTable = tables[0].Id;
        var barTable = tables[tables.Count / 3].Id;
        var servedTable = tables[tables.Count * 2 / 3].Id;

        var kitchen = new Adisyon { TableId = kitchenTable, CreatedAt = Ago(50) };
        kitchen.Orders.Add(NewOrder(kitchenTable, 48, OrderStatus.ServisEdildi, ("Ayran", MenuDepartment.Bar, 2), ("Şalgam", MenuDepartment.Bar, 1)));
        kitchen.Orders.Add(NewOrder(kitchenTable, 12, OrderStatus.Hazirlaniyor, ("Adana Kebap", MenuDepartment.Mutfak, 2), ("Çoban Salata", MenuDepartment.Mutfak, 1)));

        var bar = new Adisyon { TableId = barTable, CreatedAt = Ago(20) };
        bar.Orders.Add(NewOrder(barTable, 4, OrderStatus.Yeni, ("Türk Çayı", MenuDepartment.Bar, 3), ("Ayran", MenuDepartment.Bar, 1), ("Lahmacun", MenuDepartment.Mutfak, 3)));

        var served = new Adisyon { TableId = servedTable, CreatedAt = Ago(75) };
        served.Orders.Add(NewOrder(servedTable, 70, OrderStatus.ServisEdildi, ("Karışık Izgara", MenuDepartment.Mutfak, 1), ("Humus", MenuDepartment.Mutfak, 1), ("Kola", MenuDepartment.Bar, 2)));
        served.Orders.Add(NewOrder(servedTable, 25, OrderStatus.ServisEdildi, ("Fıstıklı Baklava", MenuDepartment.Mutfak, 2), ("Türk Kahvesi", MenuDepartment.Bar, 2)));

        var discount = new DiscountRequest
        {
            Adisyon = served,
            Amount = Math.Round(served.ItemsTotal * 0.10m, 0),
            Reason = "Doğum günü kutlaması",
            RequestedByUserName = "Garson",
            CreatedAt = Ago(5)
        };
        served.DiscountRequests.Add(discount);

        var call = new WaiterCall { TableId = kitchenTable, CreatedAt = Ago(2) };
        var statuses = new[] { kitchenTable, barTable, servedTable }.Distinct().ToDictionary(id => id, _ => TableStatus.Dolu);

        return (new List<Adisyon> { kitchen, bar, served }, new List<DiscountRequest> { discount }, new List<WaiterCall> { call }, statuses);
    }

    private static List<Customer> Customers(Random rng, DateTime today)
    {
        var tiers = new (int Count, int Min, int Max)[] { (12, 200, 950), (7, 1000, 4900), (4, 5000, 14000), (2, 15000, 30000) };
        var customers = new List<Customer>();
        var index = 0;
        foreach (var (count, min, max) in tiers)
        {
            for (var i = 0; i < count; i++, index++)
            {
                var spent = (decimal)(rng.Next(min / 10, max / 10 + 1) * 10);
                var name = CustomerNames[index];
                customers.Add(new Customer
                {
                    Name = name,
                    Phone = $"5{rng.Next(30, 56)}{index:D2}{rng.Next(10000, 100000)}",
                    Email = index % 3 == 0 ? $"{SlugHelper.Slugify(name)?.Replace("-", ".")}@ornek.local" : null,
                    TotalSpent = spent,
                    Points = (int)Math.Floor(spent / 10m),
                    Level = CustomerService.LevelFor(spent),
                    CreatedAt = today.AddDays(-rng.Next(20, 240)).AddHours(rng.Next(12, 22))
                });
            }
        }

        return customers;
    }

    private static List<Reservation> Reservations(Random rng, DateTime today)
    {
        var plan = new (int Day, string Name, int Hour, int Party, ReservationStatus Status)[]
        {
            (1, "Deniz Kılıç", 20, 4, ReservationStatus.Onaylandi), (1, "Serkan Polat", 13, 2, ReservationStatus.Beklemede),
            (2, "Ayşe Yılmaz", 19, 6, ReservationStatus.Onaylandi), (3, "Kerem Kurt", 20, 2, ReservationStatus.IptalEdildi),
            (4, "Buse Aksoy", 21, 3, ReservationStatus.Onaylandi), (5, "Murat Bulut", 20, 8, ReservationStatus.Onaylandi),
            (6, "İrem Korkmaz", 19, 4, ReservationStatus.Beklemede), (7, "Emre Öztürk", 13, 5, ReservationStatus.Onaylandi),
            (0, "Gizem Koç", 21, 4, ReservationStatus.Onaylandi), (-2, "Derya Şimşek", 20, 4, ReservationStatus.Onaylandi), (-6, "Mehmet Kaya", 19, 2, ReservationStatus.Onaylandi),
            (-11, "Elif Şahin", 21, 6, ReservationStatus.Onaylandi), (-19, "Cem Aslan", 20, 3, ReservationStatus.Onaylandi)
        };

        return plan.Select(r => new Reservation
        {
            Name = r.Name,
            Phone = $"5{rng.Next(30, 56)}{rng.Next(1000000, 10000000)}",
            Date = today.AddDays(r.Day),
            Time = new TimeSpan(r.Hour, rng.Next(0, 2) * 30, 0),
            PartySize = r.Party,
            Note = r.Party >= 6 ? "Bahçede uzun masa rica ediyoruz." : null,
            Status = r.Status,
            CreatedAt = today.AddDays(Math.Min(r.Day, 0) - rng.Next(1, 6)).AddHours(rng.Next(10, 22))
        }).ToList();
    }

    private static List<Feedback> Feedbacks(DateTime today, IReadOnlyList<RestaurantTable> tables) =>
        FeedbackTexts.Select((f, i) => new Feedback
        {
            Name = f.Name,
            Rating = f.Rating,
            Comment = f.Comment,
            TableId = i % 4 == 0 ? tables[i % tables.Count].Id : null,
            CreatedAt = today.AddDays(-(i * 2 + 1)).AddHours(21),
            IsPublished = i < 6,
            HomeDisplayOrder = i < 4 ? i + 1 : null
        }).ToList();

    private static List<Supplier> Suppliers() => new()
    {
        new Supplier { Name = "Kadıköy Et Ürünleri", ContactName = "Ali Usta", Phone = "02165550142", Address = "Hasanpaşa, Kadıköy / İstanbul" },
        new Supplier { Name = "Bereket Sebze Meyve", ContactName = "Hüseyin Bey", Phone = "02165550187", Address = "Acıbadem, Kadıköy / İstanbul" },
        new Supplier { Name = "Anadolu Gıda Toptan", ContactName = "Seda Hanım", Phone = "02165550163", TaxNumber = "1234567890", Address = "Kozyatağı, Kadıköy / İstanbul" }
    };

    private static (List<InventoryItem>, List<RecipeItem>, List<Purchase>, List<InventoryTransaction>) Stock(
        Random rng, DateTime today, IReadOnlyList<MenuItem> menu, List<Adisyon> adisyonlar, List<Supplier> suppliers)
    {
        var items = StockItems.ToDictionary(s => s.Name, s => new InventoryItem
        {
            Name = s.Name, Unit = s.Unit, UnitCost = s.UnitCost, MinLevel = s.MinLevel, Supplier = suppliers[s.Supplier]
        });

        var recipes = new List<RecipeItem>();
        var recipeByMenu = new Dictionary<int, (InventoryItem Item, decimal Quantity)[]>();
        foreach (var (menuName, lines) in RecipeDefinitions)
        {
            var menuItem = menu.FirstOrDefault(m => m.Name == menuName);
            if (menuItem == null) continue;
            var resolved = lines.Select(l => (items[l.Stock], l.Quantity)).ToArray();
            recipeByMenu[menuItem.Id] = resolved;
            recipes.AddRange(resolved.Select(r => new RecipeItem { MenuItemId = menuItem.Id, InventoryItem = r.Item1, Quantity = r.Quantity }));
        }

        var dailyUsage = adisyonlar
            .Where(a => a.Status == AdisyonStatus.Odendi)
            .SelectMany(a => a.Orders.SelectMany(o => o.Items).Select(i => (Day: a.ClosedAt!.Value.Date, Line: i)))
            .Where(x => recipeByMenu.ContainsKey(x.Line.MenuItemId))
            .SelectMany(x => recipeByMenu[x.Line.MenuItemId].Select(r => (x.Day, r.Item, Quantity: r.Quantity * x.Line.Quantity)))
            .GroupBy(x => (x.Day, x.Item))
            .Select(g => (g.Key.Day, g.Key.Item, Quantity: Math.Round(g.Sum(x => x.Quantity), 3)))
            .OrderBy(x => x.Day)
            .ToList();

        var purchases = new List<Purchase>
        {
            new() { Supplier = suppliers[0], PurchaseDate = today.AddDays(-20).AddHours(9), InvoiceNumber = "KEU-2026-0418" },
            new() { Supplier = suppliers[2], PurchaseDate = today.AddDays(-6).AddHours(10), InvoiceNumber = "AGT-2026-1127" }
        };

        var transactions = new List<InventoryTransaction>();
        foreach (var (name, _, _, minLevel, supplierIndex) in StockItems)
        {
            var item = items[name];
            var usage = dailyUsage.Where(u => u.Item == item).Sum(u => u.Quantity);
            var end = CriticalStock.Contains(name)
                ? Math.Round(minLevel * 0.5m, 3)
                : Math.Round(minLevel * (decimal)(1.6 + rng.NextDouble() * 1.4), 3);

            var purchase = supplierIndex == 0 ? purchases[0] : supplierIndex == 2 ? purchases[1] : null;
            var bought = purchase != null && usage > 0 ? Math.Ceiling(usage * 0.4m) : 0m;
            var opening = end - bought + usage;

            transactions.Add(new InventoryTransaction
            {
                InventoryItem = item, Type = InventoryTransactionType.ManuelDuzeltme, QuantityChange = opening,
                CreatedAt = today.AddDays(-PastDays).AddHours(8), Note = "Açılış stok sayımı"
            });

            if (bought > 0)
            {
                purchase!.Items.Add(new PurchaseItem { InventoryItem = item, Quantity = bought, UnitPrice = item.UnitCost });
                transactions.Add(new InventoryTransaction
                {
                    InventoryItem = item, Type = InventoryTransactionType.SatinAlma, QuantityChange = bought,
                    CreatedAt = purchase.PurchaseDate, Note = $"Satın alma {purchase.InvoiceNumber}"
                });
            }

            transactions.AddRange(dailyUsage.Where(u => u.Item == item).Select(u => new InventoryTransaction
            {
                InventoryItem = item, Type = InventoryTransactionType.SatisDususu, QuantityChange = -u.Quantity,
                CreatedAt = u.Day.AddHours(23).AddMinutes(30), Note = "Günlük satış düşümü"
            }));

            item.CurrentQuantity = transactions.Where(t => t.InventoryItem == item).Sum(t => t.QuantityChange);
        }

        return (items.Values.ToList(), recipes, purchases, transactions);
    }

    private static Campaign Campaign(DateTime today) => new()
    {
        Name = "Hafta İçi Öğle Menüsü",
        Description = "Hafta içi 12:00–15:00 arası tüm kebap ve ana yemeklerde %15 indirim.",
        DiscountType = CampaignDiscountType.Yuzde,
        Value = 15,
        StartDate = today.AddDays(-10),
        EndDate = today.AddDays(20),
        IsActive = true,
        ButtonText = "Menüyü İncele",
        ButtonUrl = "/menu",
        DisplayOrder = 1
    };

    private static List<ChefSpecial> ChefSpecials(DateTime today, IReadOnlyList<MenuItem> menu) =>
        new[]
        {
            ("Karışık Izgara", "Adana, kuzu şiş, tavuk kanat ve köfte; közlenmiş domates ve biberle."),
            ("Fıstıklı Baklava", "Antep fıstığıyla, her sabah taze açılan yufkadan.")
        }
        .Select(s => (Item: menu.FirstOrDefault(m => m.Name == s.Item1), Description: s.Item2))
        .Where(s => s.Item != null)
        .Select(s => new ChefSpecial
        {
            MenuItemId = s.Item!.Id,
            DescriptionOverride = s.Description,
            StartDate = today.AddDays(-3),
            EndDate = today.AddDays(14),
            IsActive = true
        }).ToList();
}
