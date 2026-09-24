using System.Security.Claims;
using RestoranYonetim.Security;

namespace RestoranYonetim.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static bool IsManagement(this ClaimsPrincipal user) =>
        user.IsInRole(RolePermissions.SuperAdmin) ||
        user.IsInRole(RolePermissions.RestoranSahibi) ||
        user.IsInRole(RolePermissions.Mudur);

    public static bool IsOperational(this ClaimsPrincipal user) =>
        user.IsManagement() ||
        user.IsInRole(RolePermissions.Garson) ||
        user.IsInRole(RolePermissions.Kasiyer) ||
        user.IsInRole(RolePermissions.MutfakPersoneli) ||
        user.IsInRole(RolePermissions.BarPersoneli);
}
