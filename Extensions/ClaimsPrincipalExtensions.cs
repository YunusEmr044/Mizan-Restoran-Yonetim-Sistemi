using System.Security.Claims;
using RestoranYonetim.Security;

namespace RestoranYonetim.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static bool IsManagement(this ClaimsPrincipal user) =>
        user.IsInRole(RolePermissions.SuperAdmin) ||
        user.IsInRole(RolePermissions.RestoranSahibi) ||
        user.IsInRole(RolePermissions.Mudur);

    public static HashSet<string> GetPermissions(this ClaimsPrincipal user) =>
        user.FindAll(Permissions.ClaimType).Select(c => c.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);

    public static bool IsOperational(this ClaimsPrincipal user) =>
        user.IsManagement() ||
        user.IsInRole(RolePermissions.Garson) ||
        user.IsInRole(RolePermissions.Kasiyer) ||
        user.IsInRole(RolePermissions.MutfakPersoneli) ||
        user.IsInRole(RolePermissions.BarPersoneli);
}
