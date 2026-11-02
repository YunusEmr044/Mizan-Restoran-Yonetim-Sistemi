using RestoranYonetim.Models;
using RestoranYonetim.Security;
using RestoranYonetim.Services;

namespace RestoranYonetim.Tests;

public class PackageRulesTests
{
    [Theory]
    [InlineData(OrderType.Masa, 5, "Masa 5", null, "Masa 5")]
    [InlineData(OrderType.GelAl, 1234, null, "Ahmet", "GEL-AL #1234 · Ahmet")]
    [InlineData(OrderType.AdreseTeslim, 7, null, null, "TESLİM #7")]
    [InlineData(OrderType.AdreseTeslim, 7, null, "  ", "TESLİM #7")]
    [InlineData(OrderType.Masa, 9, null, null, "Masa")]
    public void Label(OrderType type, int id, string? table, string? customer, string expected)
    {
        Assert.Equal(expected, AdisyonLabel.For(type, id, table, customer));
    }

    [Fact]
    public void Label_FromAdisyon()
    {
        var a = new Adisyon { Id = 3, OrderType = OrderType.GelAl, Package = new PackageInfo { CustomerName = "Ece" } };
        Assert.Equal("GEL-AL #3 · Ece", AdisyonLabel.For(a));
        Assert.Equal("Masa 2", AdisyonLabel.For(new Adisyon { Id = 4, Table = new RestaurantTable { Name = "Masa 2" } }));
    }

    [Fact]
    public void Validate_Takeaway()
    {
        Assert.NotNull(PackageValidation.Validate(OrderType.GelAl, new PackageInfo()));
        Assert.Null(PackageValidation.Validate(OrderType.GelAl, new PackageInfo { CustomerName = "Ahmet" }));
        Assert.NotNull(PackageValidation.Validate(OrderType.GelAl, new PackageInfo { CustomerName = new string('a', 151) }));
        Assert.NotNull(PackageValidation.Validate(OrderType.GelAl, new PackageInfo { CustomerName = "Ahmet", Note = new string('n', 301) }));
    }

    [Fact]
    public void Validate_Delivery()
    {
        Assert.NotNull(PackageValidation.Validate(OrderType.AdreseTeslim, new PackageInfo { CustomerName = "Ahmet", Address = "Moda Cad. 1" }));
        Assert.NotNull(PackageValidation.Validate(OrderType.AdreseTeslim, new PackageInfo { CustomerName = "Ahmet", CustomerPhone = "05321234567" }));
        Assert.Null(PackageValidation.Validate(OrderType.AdreseTeslim, new PackageInfo { CustomerName = "Ahmet", CustomerPhone = "05321234567", Address = "Moda Cad. 1" }));
        Assert.NotNull(PackageValidation.Validate(OrderType.AdreseTeslim, new PackageInfo { CustomerName = "Ahmet", CustomerPhone = "05321234567", Address = new string('x', 501) }));
    }

    [Fact]
    public void Validate_TableOrderNeedsNothing()
    {
        Assert.Null(PackageValidation.Validate(OrderType.Masa, new PackageInfo()));
    }

    [Fact]
    public void Modules_DefaultStates()
    {
        Assert.True(FeatureCatalog.All.Single(f => f.Key == "takeaway").DefaultEnabled);
        Assert.False(FeatureCatalog.All.Single(f => f.Key == "delivery").DefaultEnabled);
        Assert.All(FeatureCatalog.All.Where(f => f.Key is not ("delivery" or "kitchen-printer" or "fiscal")), f => Assert.True(f.DefaultEnabled, f.Key));
        Assert.Equal("delivery", FeatureCatalog.FindForPermission(Permissions.Delivery.Create)?.Key);
        Assert.Equal("takeaway", FeatureCatalog.FindForPermission(Permissions.Takeaway.Create)?.Key);
        Assert.Equal("orders", FeatureCatalog.FindForPermission(Permissions.Orders.Transfer)?.Key);
    }

    [Theory]
    [InlineData(RolePermissions.Garson)]
    [InlineData(RolePermissions.Kasiyer)]
    [InlineData(RolePermissions.Mudur)]
    public void Roles_GetTransferAndPackagePermissions(string role)
    {
        var defaults = RolePermissions.Defaults[role];
        Assert.Contains(Permissions.Orders.Transfer, defaults);
        Assert.Contains(Permissions.Takeaway.Create, defaults);
        Assert.Contains(Permissions.Delivery.Create, defaults);
    }
}

public class PackagePermissionTests
{
    [Fact]
    public void RequiredPermission_PerType()
    {
        Assert.Equal(Permissions.Takeaway.Create, PackageValidation.RequiredPermission(OrderType.GelAl));
        Assert.Equal(Permissions.Delivery.Create, PackageValidation.RequiredPermission(OrderType.AdreseTeslim));
        Assert.Null(PackageValidation.RequiredPermission(OrderType.Masa));
    }

    [Fact]
    public void FeatureKey_MatchesCatalog()
    {
        Assert.Equal("takeaway", PackageValidation.FeatureKey(OrderType.GelAl));
        Assert.Equal("delivery", PackageValidation.FeatureKey(OrderType.AdreseTeslim));
        Assert.Null(PackageValidation.FeatureKey(OrderType.Masa));
        Assert.All(new[] { OrderType.GelAl, OrderType.AdreseTeslim }, t =>
            Assert.Equal(FeatureCatalog.FindForPermission(PackageValidation.RequiredPermission(t)!)!.Key, PackageValidation.FeatureKey(t)));
    }

    [Fact]
    public void PromisedAt_ParsesTodayOrTomorrow()
    {
        var now = new DateTime(2026, 11, 4, 20, 0, 0);
        Assert.Equal(new DateTime(2026, 11, 4, 20, 45, 0), PackageValidation.PromisedAt("20:45", now));
        Assert.Equal(new DateTime(2026, 11, 5, 9, 30, 0), PackageValidation.PromisedAt("09:30", now));
        Assert.Null(PackageValidation.PromisedAt("", now));
        Assert.Null(PackageValidation.PromisedAt("25:99", now));
    }
}

public class AdisyonLabelOrderTests
{
    [Fact]
    public void Order_Uses_Its_Table_When_Adisyon_Table_Is_Not_Loaded()
    {
        var order = new Order { Table = new RestaurantTable { Name = "Masa 7" }, Adisyon = new Adisyon { OrderType = OrderType.Masa } };

        Assert.Equal("Masa 7", AdisyonLabel.For(order));
    }
}
