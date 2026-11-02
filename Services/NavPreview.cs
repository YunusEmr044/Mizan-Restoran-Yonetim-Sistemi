using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using RestoranYonetim.Data;
using RestoranYonetim.Security;

namespace RestoranYonetim.Services;

// Süper Admin menüyü ve dashboard'u başka bir rolün gözüyle önizleyebilir; yalnızca görünüm değişir, oturum ve yetkiler aynı kalır.
public static class NavPreview
{
    public const string CookieName = "ry-nav-preview";

    public static string? RoleFor(ClaimsPrincipal user, string? requested) =>
        user.IsInRole(RolePermissions.SuperAdmin) && requested != RolePermissions.SuperAdmin && RolePermissions.AllRoles.Contains(requested)
            ? requested
            : null;

    public static string? RoleFor(HttpContext http) => RoleFor(http.User, http.Request.Cookies[CookieName]);

    public static async Task<ClaimsPrincipal?> PreviewPrincipalAsync(HttpContext http, ApplicationDbContext context) =>
        RoleFor(http) is { } role ? PrincipalFor(role, await PermissionResolver.ResolveAsync(context, new[] { role })) : null;

    public static ClaimsPrincipal PrincipalFor(string role, IEnumerable<string> permissions) =>
        new(new ClaimsIdentity(
            permissions.Select(p => new Claim(Permissions.ClaimType, p)).Prepend(new Claim(ClaimTypes.Role, role)),
            "nav-preview"));
}
