using System.Security.Claims;
using Microsoft.AspNetCore.Routing;
using RestoranYonetim.Security;

namespace RestoranYonetim.Services;

public sealed record NavItem(string Title, string Icon, string Area, string Controller, string Action = "Index",
    string? Permission = null, string? Role = null, string[]? ExtraControllers = null)
{
    public bool IsVisibleTo(ClaimsPrincipal user) =>
        (Permission == null || user.HasClaim(Permissions.ClaimType, Permission)) &&
        (Role == null || user.IsInRole(Role));

    public bool Matches(string? area, string? controller) =>
        string.Equals(Area, area ?? string.Empty, StringComparison.OrdinalIgnoreCase) &&
        (string.Equals(Controller, controller, StringComparison.OrdinalIgnoreCase) ||
         (ExtraControllers?.Any(c => string.Equals(c, controller, StringComparison.OrdinalIgnoreCase)) ?? false));
}

public sealed record NavGroup(string Key, string Title, IReadOnlyList<NavItem> Items);

// Panel menüsü, menü araması ve gezinti yolu bu listeden beslenir. Gereksinimler ilgili controller'ın [Authorize]'ı ile aynı tutulmalı.
public static class AdminNavigation
{
    private const string Admin = "Admin";

    public static IReadOnlyList<NavGroup> All { get; } = new List<NavGroup>
    {
        new("ops", "Operasyon", new NavItem[]
        {
            new("Dashboard", "bi-speedometer2", Admin, "Dashboard", Permission: Permissions.Reports.View),
            new("Sipariş Ekranı", "bi-clipboard-check", "", "Staff", Permission: Permissions.Orders.View),
            new("Paketler", "bi-box-seam", "", "Staff", "Paketler", Permissions.Orders.View),
            new("Mutfak", "bi-egg-fried", "", "Mutfak", Permission: Permissions.Orders.Kitchen),
            new("Bar", "bi-cup-straw", "", "Bar", Permission: Permissions.Orders.Bar),
            new("Kasa", "bi-cash-coin", "", "Kasa", Permission: Permissions.Cash.View)
        }),
        new("isletme", "İşletme", new NavItem[]
        {
            new("Restoranlar", "bi-shop", Admin, "Restaurants", Permission: Permissions.Tables.Manage),
            new("Şubeler", "bi-diagram-3", Admin, "Branches", Permission: Permissions.Tables.Manage),
            new("Alanlar", "bi-bounding-box", Admin, "Areas", Permission: Permissions.Tables.Manage),
            new("Masalar / QR", "bi-qr-code", Admin, "Tables", Permission: Permissions.Tables.Manage),
            new("Yazıcılar", "bi-printer", Admin, "Printers", Permission: Permissions.Printers.Manage),
            new("Yazar Kasa", "bi-receipt-cutoff", Admin, "FiscalDevices", Permission: Permissions.Fiscal.Manage)
        }),
        new("personel", "Personel", new NavItem[]
        {
            new("Vardiya Planı", "bi-calendar-week", Admin, "Personnel", "Shifts", Permission: Permissions.Staff.Manage),
            new("Mesai", "bi-clock-history", Admin, "Personnel", "Time", Permission: Permissions.Staff.Manage),
            new("İzinler", "bi-calendar-x", Admin, "Personnel", "Leaves", Permission: Permissions.Staff.Manage)
        }),
        new("menu", "Menü", new NavItem[]
        {
            new("Kategoriler", "bi-tags", Admin, "Categories", Permission: Permissions.Products.Manage),
            new("Menü Ürünleri", "bi-journal-richtext", Admin, "MenuItems", Permission: Permissions.Products.Manage)
        }),
        new("satis", "Satış ve Raporlar", new NavItem[]
        {
            new("Tüm Siparişler", "bi-receipt", Admin, "Orders", Permission: Permissions.Reports.View),
            new("Raporlar", "bi-bar-chart-line", Admin, "Reports", Permission: Permissions.Reports.View)
        }),
        new("stok", "Stok", new NavItem[]
        {
            new("Stok Kalemleri", "bi-box-seam", Admin, "Inventory", Permission: Permissions.Inventory.View),
            new("Tedarikçiler", "bi-truck", Admin, "Suppliers", Permission: Permissions.Inventory.Manage),
            new("Satın Almalar", "bi-cart-check", Admin, "Purchases", Permission: Permissions.Inventory.Manage)
        }),
        new("musteri", "Müşteri", new NavItem[]
        {
            new("Rezervasyonlar", "bi-calendar-check", Admin, "Reservations", Permission: Permissions.Reservations.View),
            new("Müşteriler", "bi-people", Admin, "Customers", Permission: Permissions.Customers.View),
            new("Geri Bildirimler", "bi-star", Admin, "Feedback", Permission: Permissions.Feedback.View),
            new("Kampanyalar", "bi-megaphone", Admin, "Campaigns", Permission: Permissions.Campaigns.View)
        }),
        new("site", "Site", new NavItem[]
        {
            new("Site İçeriği", "bi-window", Admin, "SiteContent", Permission: Permissions.SiteContent.Manage),
            new("Galeri", "bi-images", Admin, "Gallery", Permission: Permissions.SiteContent.Manage),
            new("Çeviriler", "bi-translate", Admin, "Translations", Permission: Permissions.SiteContent.Manage),
            new("Ana Sayfa Düzeni", "bi-layout-text-window", Admin, "HomePage", "Sections", Permissions.SiteContent.Manage, ExtraControllers: new[] { "ChefSpecial" }),
            new("SEO Ayarları", "bi-graph-up-arrow", Admin, "SeoSettings", Permission: Permissions.SiteContent.Manage, ExtraControllers: new[] { "Faq" }),
            new("Yasal Sayfalar", "bi-file-earmark-text", Admin, "LegalPages", Permission: Permissions.SiteContent.Manage)
        }),
        new("sistem", "Sistem", new NavItem[]
        {
            new("Kurulum", "bi-rocket-takeoff", Admin, "Setup", Permission: Permissions.Settings.Manage),
            new("Kullanıcılar", "bi-person-gear", Admin, "Users", Permission: Permissions.Users.Manage),
            new("Roller", "bi-person-badge", Admin, "Roles", Role: RolePermissions.SuperAdmin),
            new("Özellikler ve İzinler", "bi-toggles", Admin, "Features", Role: RolePermissions.SuperAdmin),
            new("Ayarlar", "bi-gear", Admin, "Settings", Role: RolePermissions.SuperAdmin),
            new("Loglar", "bi-clock-history", Admin, "Logs", Role: RolePermissions.SuperAdmin)
        })
    };

    public static IReadOnlyList<NavGroup> For(ClaimsPrincipal user) => All
        .Select(g => g with { Items = g.Items.Where(i => i.IsVisibleTo(user)).ToList() })
        .Where(g => g.Items.Count > 0)
        .ToList();

    public static (NavGroup Group, NavItem Item)? Find(RouteValueDictionary route) =>
        Find(route["area"]?.ToString(), route["controller"]?.ToString(), route["action"]?.ToString());

    // Aynı controller'da birden çok öğe varsa eylemi birebir eşleşen öğe seçilir.
    public static (NavGroup Group, NavItem Item)? Find(string? area, string? controller, string? action = null)
    {
        (NavGroup Group, NavItem Item)? first = null;
        foreach (var group in All)
        {
            foreach (var item in group.Items.Where(i => i.Matches(area, controller)))
            {
                if (string.Equals(item.Action, action, StringComparison.OrdinalIgnoreCase))
                {
                    return (group, item);
                }
                first ??= (group, item);
            }
        }
        return first;
    }
}
