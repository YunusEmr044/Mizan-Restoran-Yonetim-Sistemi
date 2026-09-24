using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestoranYonetim.Data;
using RestoranYonetim.Models;
using RestoranYonetim.Security;
using RestoranYonetim.Services;

namespace RestoranYonetim.Areas.Admin.Controllers;

public class FeatureListItem
{
    public FeatureSetting Setting { get; set; } = null!;
    public int PermissionCount { get; set; }
}

public class RolePermissionListItem
{
    public string RoleName { get; set; } = string.Empty;
    public IReadOnlyList<string> EnabledPermissions { get; set; } = [];
}

public class FeaturesViewModel
{
    public List<FeatureListItem> Features { get; set; } = [];
    public List<RolePermissionListItem> Roles { get; set; } = [];
    public IReadOnlyList<string> AllPermissions { get; set; } = [];
}

[Area("Admin")]
[Authorize(Roles = RolePermissions.SuperAdmin)]
public class FeaturesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly PermissionCacheVersion _cacheVersion;
    private readonly AuditService _audit;

    public FeaturesController(ApplicationDbContext context, RoleManager<IdentityRole> roleManager, PermissionCacheVersion cacheVersion, AuditService audit)
    {
        _context = context;
        _roleManager = roleManager;
        _cacheVersion = cacheVersion;
        _audit = audit;
    }

    public async Task<IActionResult> Index()
    {
        await EnsureDefaultsAsync();
        var settings = await _context.FeatureSettings.AsNoTracking().OrderBy(f => f.DisplayName).ToListAsync();
        var rolePermissions = await _context.RolePermissions.AsNoTracking().ToListAsync();

        // Roller/Yeni Rol ile eklenen özel roller de buraya, sistemin 10 sabit rolüyle birlikte gelir -
        // burası tek izin yönetim ekranı, hangi rolün nasıl oluşturulduğu fark etmez.
        var roleNames = await _roleManager.Roles.Where(r => r.Name != null).Select(r => r.Name!).ToListAsync();

        var model = new FeaturesViewModel
        {
            Features = settings.Select(setting => new FeatureListItem
            {
                Setting = setting,
                PermissionCount = Permissions.All.Count(permission => FeatureCatalog.IsEnabled(setting, permission))
            }).ToList(),
            Roles = roleNames
                .OrderBy(RolePermissions.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .Select(role => new RolePermissionListItem
                {
                    RoleName = role,
                    EnabledPermissions = rolePermissions
                        .Where(permission => permission.RoleName == role && permission.IsEnabled)
                        .Select(permission => permission.Permission)
                        .ToList()
                }).ToList(),
            AllPermissions = Permissions.All
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(string key)
    {
        var setting = await _context.FeatureSettings.SingleOrDefaultAsync(f => f.Key == key);
        if (setting == null)
        {
            return NotFound();
        }

        setting.IsEnabled = !setting.IsEnabled;
        await _context.SaveChangesAsync();
        _cacheVersion.Bump();

        // ÖNEMLİ: sistem genelinde bir özelliğin açılıp kapatılması loglanmalı (SuperAdmin-only olsa da).
        await _audit.LogAsync(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            User.Identity?.Name,
            $"Özellik {(setting.IsEnabled ? "açıldı" : "kapatıldı")}: {setting.DisplayName}",
            entityName: "FeatureSetting",
            entityId: setting.Id.ToString());

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveRolePermissions(string roleName, string[] selectedPermissions)
    {
        if (!await _roleManager.RoleExistsAsync(roleName))
        {
            return BadRequest();
        }

        var validPermissions = new HashSet<string>(Permissions.All);
        var selected = selectedPermissions.Where(validPermissions.Contains).ToHashSet();
        var existing = await _context.RolePermissions.Where(p => p.RoleName == roleName).ToListAsync();

        foreach (var permission in Permissions.All)
        {
            var row = existing.SingleOrDefault(p => p.Permission == permission);
            if (row == null)
            {
                _context.RolePermissions.Add(new RolePermission
                {
                    RoleName = roleName,
                    Permission = permission,
                    IsEnabled = selected.Contains(permission)
                });
            }
            else
            {
                row.IsEnabled = selected.Contains(permission);
            }
        }

        await _context.SaveChangesAsync();
        _cacheVersion.Bump();

        // ÖNEMLİ: bu action bir rolün TÜM izin şablonunu değiştirir - o role sahip tüm kullanıcıları aynı anda etkiler, mutlaka loglanmalı.
        await _audit.LogAsync(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            User.Identity?.Name,
            $"Rol izinleri güncellendi: {RolePermissions.DisplayName(roleName)}",
            entityName: "RolePermission",
            entityId: roleName,
            details: $"{selected.Count} izin etkin: {string.Join(", ", selected.OrderBy(p => p))}");

        return RedirectToAction(nameof(Index));
    }

    private async Task EnsureDefaultsAsync()
    {
        var settings = await _context.FeatureSettings.ToListAsync();
        foreach (var feature in FeatureCatalog.All)
        {
            if (settings.Any(setting => setting.Key == feature.Key))
            {
                continue;
            }

            _context.FeatureSettings.Add(new FeatureSetting
            {
                Key = feature.Key,
                DisplayName = feature.DisplayName,
                Permission = feature.PermissionPrefix,
                IsEnabled = true
            });
        }

        if (!await _context.RolePermissions.AnyAsync())
        {
            foreach (var role in RolePermissions.AllRoles)
            {
                var defaults = RolePermissions.Defaults.GetValueOrDefault(role, []);
                foreach (var permission in Permissions.All)
                {
                    _context.RolePermissions.Add(new RolePermission
                    {
                        RoleName = role,
                        Permission = permission,
                        IsEnabled = defaults.Contains(permission)
                    });
                }
            }
        }

        await _context.SaveChangesAsync();
    }
}
