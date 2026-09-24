using System.Reflection;

namespace RestoranYonetim.Security;

// Her sabit, [Authorize(Policy = Permissions.X.Y)] şeklinde bir policy adı olarak da kullanılır (bkz. Program.cs).
public static class Permissions
{
    public const string ClaimType = "permission";

    public static class Orders
    {
        public const string View = "orders.view";
        public const string Create = "orders.create";
        public const string Cancel = "orders.cancel";
    }

    public static class Products
    {
        public const string View = "products.view";
        public const string Manage = "products.manage";
    }

    public static class Tables
    {
        public const string View = "tables.view";
        public const string Manage = "tables.manage";
    }

    public static class Users
    {
        public const string View = "users.view";
        public const string Manage = "users.manage";
        public const string Delete = "users.delete";
    }

    public static class Cash
    {
        public const string View = "cash.view";
        public const string Operate = "cash.operate";
        public const string ApprovePayment = "cash.payment.approve";
        public const string ViewReports = "cash.reports.view";
    }

    public static class Inventory
    {
        public const string View = "inventory.view";
        public const string Manage = "inventory.manage";
    }

    public static class Reports
    {
        public const string View = "reports.view";
    }

    public static class Settings
    {
        public const string Manage = "settings.manage";
    }

    public static class SiteContent
    {
        public const string Manage = "sitecontent.manage";
    }

    public static class Reservations
    {
        public const string View = "reservations.view";
        public const string Manage = "reservations.manage";
    }

    public static class Campaigns
    {
        public const string View = "campaigns.view";
        public const string Manage = "campaigns.manage";
    }

    public static class Customers
    {
        public const string View = "customers.view";
        public const string Manage = "customers.manage";
    }

    public static class Feedback
    {
        public const string View = "feedback.view";

        // Girişsiz herkese açık formdan gelen yorumlar moderasyonsuz yayınlanmasın diye görüntüleme/yayınlama izinleri ayrı tutuldu.
        public const string Manage = "feedback.manage";
    }

    // Program.cs bu liste üzerinden her izin için otomatik bir authorization policy'si kurar.
    public static IReadOnlyList<string> All { get; } = CollectAll();

    public static string DisplayName(string permission) => permission switch
    {
        Orders.View => "Siparişleri Görüntüleme",
        Orders.Create => "Sipariş Oluşturma",
        Orders.Cancel => "Sipariş İptal Etme",
        Products.View => "Ürünleri Görüntüleme",
        Products.Manage => "Ürün Yönetimi",
        Tables.View => "Masaları Görüntüleme",
        Tables.Manage => "Masa ve QR Yönetimi",
        Users.View => "Kullanıcıları Görüntüleme",
        Users.Manage => "Kullanıcı Yönetimi",
        Users.Delete => "Kullanıcı Silme",
        Cash.View => "Kasayı Görüntüleme",
        Cash.Operate => "Kasa İşlemleri",
        Cash.ApprovePayment => "Ödeme Onaylama",
        Cash.ViewReports => "Kasa Raporlarını Görüntüleme",
        Inventory.View => "Stokları Görüntüleme",
        Inventory.Manage => "Stok Yönetimi",
        Reports.View => "Raporları Görüntüleme",
        Settings.Manage => "Sistem Ayarları Yönetimi",
        SiteContent.Manage => "Site İçeriği Yönetimi",
        Reservations.View => "Rezervasyonları Görüntüleme",
        Reservations.Manage => "Rezervasyon Yönetimi",
        Campaigns.View => "Kampanyaları Görüntüleme",
        Campaigns.Manage => "Kampanya Yönetimi",
        Customers.View => "Müşterileri Görüntüleme",
        Customers.Manage => "Müşteri Yönetimi",
        Feedback.View => "Geri Bildirimleri Görüntüleme",
        Feedback.Manage => "Geri Bildirim Yayınlama (Siteye Yansıtma)",
        _ => permission
    };

    private static IReadOnlyList<string> CollectAll()
    {
        var result = new List<string>();
        foreach (var nested in typeof(Permissions).GetNestedTypes())
        {
            foreach (var field in nested.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy))
            {
                if (field.IsLiteral && field.FieldType == typeof(string))
                {
                    if (field.GetValue(null) is string value)
                    {
                        result.Add(value);
                    }
                }
            }
        }
        return result;
    }
}
