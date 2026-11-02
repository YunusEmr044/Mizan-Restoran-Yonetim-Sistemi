using RestoranYonetim.Models;

namespace RestoranYonetim.Security;

public sealed record FeatureDefinition(string Key, string DisplayName, string PermissionPrefix, bool DefaultEnabled = true);

public static class FeatureCatalog
{
    public static IReadOnlyList<FeatureDefinition> All { get; } =
    [
        new("orders", "Siparişler", "orders."),
        new("products", "Menü Ürünleri", "products."),
        new("tables", "Masalar", "tables."),
        new("users", "Kullanıcılar", "users."),
        new("cash", "Kasa", "cash."),
        new("inventory", "Stok", "inventory."),
        new("reports", "Raporlar", "reports."),
        new("settings", "Sistem Ayarları", "settings."),
        new("site-content", "Site İçeriği", "sitecontent."),
        new("reservations", "Rezervasyonlar", "reservations."),
        new("campaigns", "Kampanyalar", "campaigns."),
        new("customers", "Müşteriler", "customers."),
        new("feedback", "Geri Bildirimler", "feedback."),
        new("takeaway", "Gel-Al", "takeaway."),
        new("delivery", "Adrese Teslim", "delivery.", DefaultEnabled: false),
        new("kitchen-printer", "Mutfak Yazıcısı", "printers.", DefaultEnabled: false),
        new("fiscal", "Yazar Kasa (ÖKC)", "fiscal.", DefaultEnabled: false)
    ];

    public static FeatureDefinition? FindForPermission(string permission) =>
        All.FirstOrDefault(feature => permission.StartsWith(feature.PermissionPrefix, StringComparison.OrdinalIgnoreCase));

    public static bool IsEnabled(FeatureSetting setting, string permission) =>
        permission.StartsWith(setting.Permission, StringComparison.OrdinalIgnoreCase);
}
