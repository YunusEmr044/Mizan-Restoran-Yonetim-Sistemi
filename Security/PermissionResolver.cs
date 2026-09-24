using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RestoranYonetim.Data;
using RestoranYonetim.Models;

namespace RestoranYonetim.Security;

// Kullanıcı rollerinden etkin izin kümesini hesaplayan ortak yer (giriş ve her istekte kullanılır, drift'i önler).
public static class PermissionResolver
{
    public static async Task<HashSet<string>> ResolveAsync(
        ApplicationDbContext context,
        IEnumerable<string> roles,
        ILogger? logger = null)
    {
        var roleArray = roles
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (roleArray.Length == 0)
        {
            return permissions;
        }

        List<RolePermission> rolePermissionRows;
        try
        {
            rolePermissionRows = await context.RolePermissions
                .Where(p => roleArray.Contains(p.RoleName))
                .ToListAsync();
        }
        catch (Exception ex)
        {
            // GÜVENLİK: fail-closed - RolePermissions okunamazsa hiçbir izin verilmez (eskiden koddaki Defaults'a düşülüyordu, bu bir admin'in kısıtladığı izni sessizce geri açabiliyordu).
            logger?.LogError(ex, "GÜVENLİK: RolePermissions tablosu okunamadı - fail-closed davranışı uygulanıyor, " +
                "bu istekte roller için HİÇBİR izin verilmeyecek (roller: {Roles}).", string.Join(",", roleArray));
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        foreach (var role in roleArray)
        {
            var rowsForRole = rolePermissionRows
                .Where(p => string.Equals(p.RoleName, role, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (rowsForRole.Count > 0)
            {
                foreach (var row in rowsForRole.Where(r => r.IsEnabled))
                {
                    permissions.Add(row.Permission);
                }
            }
            else if (RolePermissions.Defaults.TryGetValue(role, out var defaults))
            {
                // Bu rol için DB'de satır yok (henüz seed edilmemiş) -> kod varsayılanları kullanılır.
                foreach (var permission in defaults)
                {
                    permissions.Add(permission);
                }
            }
        }

        if (!roleArray.Contains(RolePermissions.SuperAdmin, StringComparer.OrdinalIgnoreCase))
        {
            List<FeatureSetting> disabledFeatures;
            try
            {
                disabledFeatures = await context.FeatureSettings
                    .Where(f => !f.IsEnabled)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                // GÜVENLİK: fail-closed - FeatureSettings okunamazsa hiçbir izin verilmez (SuperAdmin bu bloğa girmez).
                logger?.LogError(ex, "GÜVENLİK: FeatureSettings tablosu okunamadı - fail-closed davranışı uygulanıyor, " +
                    "bu istekte SuperAdmin olmayan roller için HİÇBİR modül izni verilmeyecek.");
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            if (disabledFeatures.Count > 0)
            {
                permissions.RemoveWhere(permission => disabledFeatures.Any(feature =>
                    FeatureCatalog.IsEnabled(feature, permission)));
            }
        }

        return permissions;
    }
}
