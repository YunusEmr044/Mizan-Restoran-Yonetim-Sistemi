using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestoranYonetim.Data;
using RestoranYonetim.Models;
using RestoranYonetim.Security;
using RestoranYonetim.Services;

namespace RestoranYonetim.Areas.Admin.Controllers;

public class RoleListItem
{
    public string Name { get; set; } = string.Empty;
    public int PermissionCount { get; set; }
    public int UserCount { get; set; }
    public bool IsBuiltIn { get; set; }
}

// Özel rol oluşturma/silme - sistemin 10 sabit rolüne ek olarak, kullanıcı kendi rolünü adlandırıp izinlerini
// seçebilir. Rolün kendisi ApplicationUserRole/AspNetRoles tablosunda, izinleri RolePermission'da tutulur -
// bu ikisi zaten rol adına göre çalıştığı için, sistem rolleri de özel roller de aynı altyapıyı paylaşır.
[Area("Admin")]
[Authorize(Roles = RolePermissions.SuperAdmin)]
public class RolesController : Controller
{
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _context;
    private readonly PermissionCacheVersion _cacheVersion;
    private readonly AuditService _audit;

    public RolesController(RoleManager<IdentityRole> roleManager, UserManager<ApplicationUser> userManager, ApplicationDbContext context, PermissionCacheVersion cacheVersion, AuditService audit)
    {
        _roleManager = roleManager;
        _userManager = userManager;
        _context = context;
        _cacheVersion = cacheVersion;
        _audit = audit;
    }

    public async Task<IActionResult> Index()
    {
        var roleNames = await _roleManager.Roles.Where(r => r.Name != null).Select(r => r.Name!).ToListAsync();
        var enabledPermissions = await _context.RolePermissions.Where(p => p.IsEnabled).ToListAsync();

        var items = new List<RoleListItem>();
        foreach (var name in roleNames)
        {
            items.Add(new RoleListItem
            {
                Name = name,
                PermissionCount = enabledPermissions.Count(p => p.RoleName == name),
                UserCount = (await _userManager.GetUsersInRoleAsync(name)).Count,
                IsBuiltIn = RolePermissions.AllRoles.Contains(name)
            });
        }

        return View(items.OrderBy(i => RolePermissions.DisplayName(i.Name), StringComparer.CurrentCultureIgnoreCase).ToList());
    }

    public IActionResult Create()
    {
        ViewBag.AllPermissions = Permissions.All;
        ViewBag.EnabledPermissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string name, string[] selectedPermissions)
    {
        name = (name ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            ModelState.AddModelError(string.Empty, "Rol adı gereklidir.");
        }
        else if (name.Length > 64)
        {
            ModelState.AddModelError(string.Empty, "Rol adı en fazla 64 karakter olabilir.");
        }
        else if (await _roleManager.RoleExistsAsync(name))
        {
            ModelState.AddModelError(string.Empty, "Bu isimde bir rol zaten var.");
        }

        var validPermissions = new HashSet<string>(Permissions.All);
        var selected = (selectedPermissions ?? Array.Empty<string>()).Where(validPermissions.Contains).ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (!ModelState.IsValid)
        {
            ViewBag.AllPermissions = Permissions.All;
            ViewBag.EnabledPermissions = selected;
            ViewBag.Name = name;
            return View();
        }

        var createResult = await _roleManager.CreateAsync(new IdentityRole(name));
        if (!createResult.Succeeded)
        {
            foreach (var error in createResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            ViewBag.AllPermissions = Permissions.All;
            ViewBag.EnabledPermissions = selected;
            ViewBag.Name = name;
            return View();
        }

        // Aynı isimle daha önce silinmiş bir rolden kalmış izin satırları olabilir - (RoleName, Permission)
        // benzersiz indeksi yüzünden eklemeden önce temizlenmezse kayıt patlar ve rol izinsiz kalır.
        var leftoverRows = await _context.RolePermissions.Where(p => p.RoleName == name).ToListAsync();
        if (leftoverRows.Count > 0)
        {
            _context.RolePermissions.RemoveRange(leftoverRows);
            await _context.SaveChangesAsync();
        }

        foreach (var permission in Permissions.All)
        {
            _context.RolePermissions.Add(new RolePermission
            {
                RoleName = name,
                Permission = permission,
                IsEnabled = selected.Contains(permission)
            });
        }

        await _context.SaveChangesAsync();
        _cacheVersion.Bump();

        // Yeni bir rol tanımlamak (hangi izinlerle başladığı dahil) izlenebilir olmalı.
        await _audit.LogAsync(
            _userManager.GetUserId(User),
            User.Identity?.Name,
            "Yeni rol oluşturuldu",
            entityName: "IdentityRole",
            entityId: name,
            details: $"{name}: {selected.Count} izin etkin");

        TempData["Info"] = $"\"{name}\" rolü oluşturuldu.";
        return RedirectToAction(nameof(Index));
    }

    // İzinleri düzenlemek için ayrı bir ekran açmıyoruz - "Özellikler ve İzinler" (Admin/Features) zaten
    // her rolü (artık bu ekranda oluşturulan özel roller dahil) listeliyor ve izin kutucukları oradan yönetiliyor.
    // İki ayrı yerden aynı şeyi yönetmek karışıklığa yol açar.

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return NotFound();
        }

        // GÜVENLİK: sistemin 10 sabit rolü (SuperAdmin dahil) buradan silinemez - uygulamanın birçok yerinde
        // (giriş yönlendirmesi, izin varsayılanları) isimle sabit kodlanmışlar, silinirse uygulama bozulur.
        if (RolePermissions.AllRoles.Contains(name))
        {
            TempData["Error"] = "Sistem rolleri silinemez.";
            return RedirectToAction(nameof(Index));
        }

        var role = await _roleManager.FindByNameAsync(name);
        if (role == null)
        {
            return NotFound();
        }

        var usersInRole = await _userManager.GetUsersInRoleAsync(name);
        if (usersInRole.Count > 0)
        {
            TempData["Error"] = $"Bu role sahip {usersInRole.Count} kullanıcı var - silmeden önce onları başka bir role taşı.";
            return RedirectToAction(nameof(Index));
        }

        var deleteResult = await _roleManager.DeleteAsync(role);
        if (!deleteResult.Succeeded)
        {
            TempData["Error"] = string.Join(" ", deleteResult.Errors.Select(e => e.Description));
            return RedirectToAction(nameof(Index));
        }

        var permissionRows = await _context.RolePermissions.Where(p => p.RoleName == name).ToListAsync();
        _context.RolePermissions.RemoveRange(permissionRows);
        await _context.SaveChangesAsync();
        _cacheVersion.Bump();

        await _audit.LogAsync(
            _userManager.GetUserId(User),
            User.Identity?.Name,
            "Rol silindi",
            entityName: "IdentityRole",
            entityId: name,
            details: name);

        TempData["Info"] = $"\"{name}\" rolü silindi.";
        return RedirectToAction(nameof(Index));
    }
}
