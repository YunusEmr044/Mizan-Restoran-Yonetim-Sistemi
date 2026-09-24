namespace RestoranYonetim.Security;

// Sistemdeki 10 rol ve her birinin varsayılan izin seti.
public static class RolePermissions
{
    public const string SuperAdmin = "SuperAdmin";
    public const string RestoranSahibi = "RestoranSahibi";
    public const string Mudur = "Mudur";
    public const string Garson = "Garson";
    public const string Kasiyer = "Kasiyer";
    public const string MutfakPersoneli = "MutfakPersoneli";
    public const string BarPersoneli = "BarPersoneli";
    public const string DepoPersoneli = "DepoPersoneli";
    public const string Muhasebe = "Muhasebe";
    public const string RaporGoruntuleyici = "RaporGoruntuleyici";

    public static readonly string[] AllRoles =
    {
        SuperAdmin, RestoranSahibi, Mudur, Garson, Kasiyer,
        MutfakPersoneli, BarPersoneli, DepoPersoneli, Muhasebe, RaporGoruntuleyici
    };

    public static string DisplayName(string role) => role switch
    {
        SuperAdmin => "Süper Admin",
        RestoranSahibi => "Restoran Sahibi",
        Mudur => "Müdür",
        Garson => "Garson",
        Kasiyer => "Kasiyer",
        MutfakPersoneli => "Mutfak Personeli",
        BarPersoneli => "Bar Personeli",
        DepoPersoneli => "Depo Personeli",
        Muhasebe => "Muhasebe",
        RaporGoruntuleyici => "Rapor Görüntüleyici",
        _ => role
    };

    public static readonly string[] ManagementRoles = { SuperAdmin, RestoranSahibi, Mudur };

    public static readonly string[] OperationalRoles =
    {
        SuperAdmin, RestoranSahibi, Mudur, Garson, Kasiyer, MutfakPersoneli, BarPersoneli
    };

    // RestoranSahibi, SuperAdmin'e yakın ama kullanıcı SİLME yetkisi yok (geri dönülemez işlem yapmasın diye); Mudur operasyonel yetkilere sahip ama kullanıcı/site/kampanya YÖNETİMİ gibi idari kararlar işletme sahibine bırakılmış.
    public static readonly Dictionary<string, string[]> Defaults = new()
    {
        [SuperAdmin] = Permissions.All.ToArray(),
        [RestoranSahibi] = Permissions.All.Where(p => p != Permissions.Users.Delete).ToArray(),
        [Mudur] = new[]
        {
            Permissions.Orders.View, Permissions.Orders.Create, Permissions.Orders.Cancel,
            Permissions.Products.View, Permissions.Products.Manage,
            Permissions.Tables.View, Permissions.Tables.Manage,
            Permissions.Users.View,
            Permissions.Cash.View, Permissions.Cash.Operate, Permissions.Cash.ApprovePayment, Permissions.Cash.ViewReports,
            Permissions.Inventory.View, Permissions.Inventory.Manage,
            Permissions.Reports.View,
            Permissions.Reservations.View, Permissions.Reservations.Manage,
            Permissions.Campaigns.View,
            Permissions.Customers.View,
            Permissions.Feedback.View, Permissions.Feedback.Manage
        },
        [Garson] = new[]
        {
            Permissions.Orders.View, Permissions.Orders.Create,
            Permissions.Tables.View
        },
        [Kasiyer] = new[]
        {
            Permissions.Orders.View,
            Permissions.Cash.View, Permissions.Cash.Operate, Permissions.Cash.ApprovePayment
        },
        [MutfakPersoneli] = new[]
        {
            Permissions.Orders.View
        },
        [BarPersoneli] = new[]
        {
            Permissions.Orders.View
        },
        [DepoPersoneli] = new[]
        {
            Permissions.Inventory.View, Permissions.Inventory.Manage
        },
        [Muhasebe] = new[]
        {
            Permissions.Reports.View, Permissions.Cash.ViewReports
        },
        [RaporGoruntuleyici] = new[]
        {
            Permissions.Reports.View
        }
    };
}
