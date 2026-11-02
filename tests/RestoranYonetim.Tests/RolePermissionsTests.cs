using RestoranYonetim.Security;

namespace RestoranYonetim.Tests;

public class RolePermissionsTests
{
    [Fact]
    public void Every_Permission_Has_A_DisplayName()
    {
        Assert.All(Permissions.All, p => Assert.NotEqual(p, Permissions.DisplayName(p)));
    }

    [Theory]
    [InlineData(RolePermissions.MutfakPersoneli, Permissions.Orders.Kitchen)]
    [InlineData(RolePermissions.BarPersoneli, Permissions.Orders.Bar)]
    public void PrepStaff_Get_Only_Their_Screen(string role, string permission)
    {
        Assert.Equal(new[] { permission }, RolePermissions.Defaults[role]);
    }

    [Theory]
    [InlineData(new[] { Permissions.Orders.View, Permissions.Orders.Kitchen }, "/Staff")]
    [InlineData(new[] { Permissions.Orders.Kitchen }, "/Mutfak")]
    [InlineData(new[] { Permissions.Orders.Bar }, "/Bar")]
    [InlineData(new[] { Permissions.Cash.View }, "/Kasa")]
    [InlineData(new string[0], null)]
    public void OperationalLanding_Picks_First_Allowed_Screen(string[] permissions, string? expected)
    {
        Assert.Equal(expected, RolePermissions.OperationalLanding(permissions.ToHashSet()));
    }
}
