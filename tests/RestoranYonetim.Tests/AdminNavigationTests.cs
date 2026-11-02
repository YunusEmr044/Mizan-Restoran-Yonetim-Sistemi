using System.Security.Claims;
using RestoranYonetim.Security;
using RestoranYonetim.Services;

namespace RestoranYonetim.Tests;

public class AdminNavigationTests
{
    private static ClaimsPrincipal As(string role)
    {
        var claims = new List<Claim> { new(ClaimTypes.Role, role) };
        claims.AddRange(RolePermissions.Defaults[role].Select(p => new Claim(Permissions.ClaimType, p)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    private static List<string> Titles(string role) =>
        AdminNavigation.For(As(role)).SelectMany(g => g.Items).Select(i => i.Title).ToList();

    [Fact]
    public void Mudur_DoesNotSee_Users_Or_Site()
    {
        var groups = AdminNavigation.For(As(RolePermissions.Mudur));
        var titles = groups.SelectMany(g => g.Items).Select(i => i.Title).ToList();

        Assert.DoesNotContain("Kullanıcılar", titles);
        Assert.DoesNotContain("Site İçeriği", titles);
        Assert.DoesNotContain("Roller", titles);
        Assert.Contains("Kampanyalar", titles);
        Assert.Subset(groups.Select(g => g.Key).ToHashSet(), new HashSet<string> { "isletme", "menu", "satis", "stok", "musteri" });
    }

    [Fact]
    public void Depo_Sees_Only_Stock()
    {
        var groups = AdminNavigation.For(As(RolePermissions.DepoPersoneli));

        var group = Assert.Single(groups);
        Assert.Equal("stok", group.Key);
        Assert.Equal(3, group.Items.Count);
    }

    [Theory]
    [InlineData(RolePermissions.Muhasebe)]
    [InlineData(RolePermissions.RaporGoruntuleyici)]
    public void Muhasebe_And_RaporGoruntuleyici_See_Reports(string role)
    {
        Assert.Equal(new[] { "Dashboard", "Tüm Siparişler", "Raporlar" }, Titles(role));
    }

    [Fact]
    public void Kasiyer_Sees_Operations_Only()
    {
        var groups = AdminNavigation.For(As(RolePermissions.Kasiyer));

        Assert.Equal("ops", Assert.Single(groups).Key);
        Assert.Equal(new[] { "Sipariş Ekranı", "Paketler", "Kasa" }, Titles(RolePermissions.Kasiyer));
    }

    [Theory]
    [InlineData(RolePermissions.MutfakPersoneli, "Mutfak")]
    [InlineData(RolePermissions.BarPersoneli, "Bar")]
    public void PrepStaff_See_Only_Their_Screen(string role, string screen)
    {
        Assert.Equal(new[] { screen }, Titles(role));
    }

    [Fact]
    public void Garson_Does_Not_See_Prep_Screens()
    {
        Assert.Equal(new[] { "Sipariş Ekranı", "Paketler" }, Titles(RolePermissions.Garson));
    }

    [Fact]
    public void Translations_Only_For_Site_Managers()
    {
        Assert.Contains("Çeviriler", Titles(RolePermissions.SuperAdmin));
        Assert.DoesNotContain("Çeviriler", Titles(RolePermissions.Mudur));
    }

    [Fact]
    public void Mudur_Sees_Fiscal()
    {
        Assert.Contains("Yazar Kasa", Titles(RolePermissions.Mudur));
        Assert.DoesNotContain("Yazar Kasa", Titles(RolePermissions.Kasiyer));
    }

    [Fact]
    public void Mudur_Sees_Printers()
    {
        Assert.Contains("Yazıcılar", Titles(RolePermissions.Mudur));
        Assert.DoesNotContain("Yazıcılar", Titles(RolePermissions.Garson));
    }

    [Fact]
    public void Mudur_Sees_Prep_Screens()
    {
        Assert.Subset(Titles(RolePermissions.Mudur).ToHashSet(), new HashSet<string> { "Mutfak", "Bar", "Paketler" });
    }

    [Fact]
    public void SuperAdmin_Sees_All()
    {
        Assert.Equal(AdminNavigation.All.Sum(g => g.Items.Count), Titles(RolePermissions.SuperAdmin).Count);
    }

    [Fact]
    public void Find_Resolves_Extra_And_NonAdmin_Controllers()
    {
        Assert.Equal("Ana Sayfa Düzeni", AdminNavigation.Find("Admin", "ChefSpecial")?.Item.Title);
        Assert.Equal("SEO Ayarları", AdminNavigation.Find("Admin", "faq")?.Item.Title);
        Assert.Equal("Kasa", AdminNavigation.Find("", "Kasa")?.Item.Title);
        Assert.Equal("Kasa", AdminNavigation.Find(null, "Kasa")?.Item.Title);
        Assert.Null(AdminNavigation.Find("Admin", "Yok"));
    }

    [Fact]
    public void Find_Prefers_Exact_Action()
    {
        Assert.Equal("Paketler", AdminNavigation.Find("", "Staff", "Paketler")?.Item.Title);
        Assert.Equal("Sipariş Ekranı", AdminNavigation.Find("", "Staff", "NewOrder")?.Item.Title);
    }

    [Fact]
    public void Definitions_Are_Consistent()
    {
        var items = AdminNavigation.All.SelectMany(g => g.Items).ToList();

        Assert.All(items.Where(i => i.Permission != null), i => Assert.Contains(i.Permission!, Permissions.All));
        Assert.Equal(AdminNavigation.All.Count, AdminNavigation.All.Select(g => g.Key).Distinct().Count());
        Assert.Equal(items.Count, items.Select(i => (i.Area.ToLowerInvariant(), i.Controller.ToLowerInvariant(), i.Action.ToLowerInvariant())).Distinct().Count());
    }
}

public class NavPreviewTests
{
    private static ClaimsPrincipal User(string role) =>
        new(new ClaimsIdentity(new[] { new Claim(ClaimTypes.Role, role) }, "test"));

    [Fact]
    public void Only_SuperAdmin_Can_Preview_A_Known_Role()
    {
        Assert.Equal(RolePermissions.Garson, NavPreview.RoleFor(User(RolePermissions.SuperAdmin), RolePermissions.Garson));
        Assert.Null(NavPreview.RoleFor(User(RolePermissions.Mudur), RolePermissions.Garson));
        Assert.Null(NavPreview.RoleFor(User(RolePermissions.SuperAdmin), "Uydurma"));
        Assert.Null(NavPreview.RoleFor(User(RolePermissions.SuperAdmin), RolePermissions.SuperAdmin));
        Assert.Null(NavPreview.RoleFor(User(RolePermissions.SuperAdmin), null));
    }

    [Fact]
    public void Preview_Principal_Sees_That_Roles_Menu()
    {
        var principal = NavPreview.PrincipalFor(RolePermissions.Garson, RolePermissions.Defaults[RolePermissions.Garson]);

        Assert.Equal(new[] { "Sipariş Ekranı", "Paketler" }, AdminNavigation.For(principal).SelectMany(g => g.Items).Select(i => i.Title));
    }
}
