using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using RestoranYonetim.Data;
using RestoranYonetim.Models;
using RestoranYonetim.Security;
using RestoranYonetim.Services;

namespace RestoranYonetim.Areas.Admin.Controllers;

public class UserListItem
{
    public ApplicationUser User { get; set; } = null!;
    public IList<string> Roles { get; set; } = new List<string>();
}

public class UserEditViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}

[Area("Admin")]
[Authorize(Policy = Permissions.Users.Manage)]
public class UsersController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ApplicationDbContext _context;
    private readonly PermissionCacheVersion _cacheVersion;
    private readonly AuditService _audit;

    public UsersController(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager, ApplicationDbContext context, PermissionCacheVersion cacheVersion, AuditService audit)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _context = context;
        _cacheVersion = cacheVersion;
        _audit = audit;
    }

    // Rol listesi artık sabit değil - RolesController üzerinden eklenen özel roller de burada görünür.
    private async Task<SelectList> GetRoleSelectListAsync(string? selected = null)
    {
        var roles = await _roleManager.Roles
            .Where(r => r.Name != null)
            .Select(r => r.Name!)
            .ToListAsync();

        var items = roles
            .OrderBy(RolePermissions.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .Select(r => new { Value = r, Text = RolePermissions.DisplayName(r) });

        return new SelectList(items, "Value", "Text", selected);
    }

    public async Task<IActionResult> Index()
    {
        var users = _userManager.Users.ToList();
        var items = new List<UserListItem>();
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            items.Add(new UserListItem { User = user, Roles = roles });
        }

        return View(items);
    }

    public async Task<IActionResult> Create()
    {
        ViewBag.Roles = await GetRoleSelectListAsync();
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string email, string fullName, string password, string role)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(role))
        {
            ModelState.AddModelError(string.Empty, "E-posta, şifre ve rol gereklidir.");
            ViewBag.Roles = await GetRoleSelectListAsync(role);
            return View();
        }

        if (!await _roleManager.RoleExistsAsync(role))
        {
            ModelState.AddModelError(string.Empty, "Geçersiz rol.");
            ViewBag.Roles = await GetRoleSelectListAsync(role);
            return View();
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FullName = fullName,
            EmailConfirmed = true
        };

        var result = await _userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            ViewBag.Roles = await GetRoleSelectListAsync(role);
            return View();
        }

        await _userManager.AddToRoleAsync(user, role);

        // Kullanıcı oluşturma ve rol ataması güvenlik açısından kritik - loglanır.
        await _audit.LogAsync(
            _userManager.GetUserId(User),
            User.Identity?.Name,
            "Kullanıcı oluşturuldu",
            entityName: "ApplicationUser",
            entityId: user.Id,
            details: $"E-posta: {email}, Rol: {RolePermissions.DisplayName(role)}");

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        var roles = await _userManager.GetRolesAsync(user);
        var currentRole = roles.FirstOrDefault() ?? string.Empty;
        ViewBag.Roles = await GetRoleSelectListAsync(currentRole);
        await LoadRolePermissionsViewBagAsync(currentRole);
        return View(new UserEditViewModel
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            FullName = user.FullName ?? string.Empty,
            Role = currentRole
        });
    }

    // NOT: izinler rol bazlıdır - burada değiştirilen izinler bu rolü paylaşan TÜM kullanıcıları etkiler, sadece görüntüleneni değil.
    private async Task LoadRolePermissionsViewBagAsync(string roleName)
    {
        if (string.IsNullOrEmpty(roleName))
        {
            ViewBag.RolePermissionsRole = null;
            return;
        }

        var enabled = await _context.RolePermissions
            .AsNoTracking()
            .Where(p => p.RoleName == roleName && p.IsEnabled)
            .Select(p => p.Permission)
            .ToListAsync();

        ViewBag.RolePermissionsRole = roleName;
        ViewBag.RolePermissionsEnabled = enabled.ToHashSet(StringComparer.OrdinalIgnoreCase);
        ViewBag.AllPermissions = Permissions.All;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveRolePermissions(string userId, string roleName, string[] selectedPermissions)
    {
        if (!await _roleManager.RoleExistsAsync(roleName))
        {
            return BadRequest();
        }

        // GÜVENLİK: yetki yükseltme koruması. Users.Manage izni SuperAdmin olmayana da verilebildiğinden, ek kısıtlama olmadan biri SuperAdmin'in izinlerini değiştirebilir ya da kendi rolüne izin ekleyip kendini yetkilendirebilirdi. Sadece SuperAdmin, SuperAdmin rolünü değiştirebilir; kimse kendi rolünü kendisi değiştiremez.
        var isSuperAdmin = User.IsInRole(RolePermissions.SuperAdmin);
        if (!isSuperAdmin && (roleName == RolePermissions.SuperAdmin || User.IsInRole(roleName)))
        {
            return Forbid();
        }

        var validPermissions = new HashSet<string>(Permissions.All);
        var selected = (selectedPermissions ?? Array.Empty<string>()).Where(validPermissions.Contains).ToHashSet();
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

        // Bir rolün izinlerinin değişmesi o rolü paylaşan TÜM kullanıcıları etkiler - loglanır.
        await _audit.LogAsync(
            _userManager.GetUserId(User),
            User.Identity?.Name,
            "Rol izinleri güncellendi",
            entityName: "RolePermission",
            entityId: roleName,
            details: $"{RolePermissions.DisplayName(roleName)}: {selected.Count} izin etkin");

        TempData["Info"] = $"{RolePermissions.DisplayName(roleName)} rolünün izinleri güncellendi.";
        return RedirectToAction(nameof(Edit), new { id = userId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(UserEditViewModel model)
    {
        if (!await _roleManager.RoleExistsAsync(model.Role))
        {
            ModelState.AddModelError(nameof(model.Role), "Geçersiz rol.");
        }

        var user = await _userManager.FindByIdAsync(model.Id);
        if (user == null)
        {
            return NotFound();
        }

        // GÜVENLİK: yetki yükseltme koruması - bu form ek kısıtlama olmadan SuperAdmin rolünü herhangi birine atayabilir ya da mevcut bir SuperAdmin'i düşürebilirdi. Sadece SuperAdmin, SuperAdmin rolü atayabilir/kaldırabilir; kimse kendi rolünü kendisi değiştiremez.
        var currentRoles = await _userManager.GetRolesAsync(user);
        var targetIsSuperAdmin = currentRoles.Contains(RolePermissions.SuperAdmin);
        var isSuperAdmin = User.IsInRole(RolePermissions.SuperAdmin);
        var isSelf = string.Equals(_userManager.GetUserId(User), user.Id, StringComparison.Ordinal);

        if (!isSuperAdmin && (model.Role == RolePermissions.SuperAdmin || targetIsSuperAdmin || (isSelf && model.Role != currentRoles.FirstOrDefault())))
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            ViewBag.Roles = await GetRoleSelectListAsync(model.Role);
            await LoadRolePermissionsViewBagAsync(model.Role);
            return View(model);
        }

        user.Email = model.Email;
        user.UserName = model.Email;
        user.FullName = model.FullName;
        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            foreach (var error in updateResult.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            ViewBag.Roles = await GetRoleSelectListAsync(model.Role);
            await LoadRolePermissionsViewBagAsync(model.Role);
            return View(model);
        }

        var previousRole = currentRoles.FirstOrDefault();
        await _userManager.RemoveFromRolesAsync(user, currentRoles);
        await _userManager.AddToRoleAsync(user, model.Role);

        // Bir kullanıcının rolünün değişmesi (özellikle SuperAdmin'e yükseltme/düşürme) izlenebilir olmalı.
        if (!string.Equals(previousRole, model.Role, StringComparison.Ordinal))
        {
            await _audit.LogAsync(
                _userManager.GetUserId(User),
                User.Identity?.Name,
                "Kullanıcı rolü değiştirildi",
                entityName: "ApplicationUser",
                entityId: user.Id,
                details: $"{user.Email}: {RolePermissions.DisplayName(previousRole ?? "-")} → {RolePermissions.DisplayName(model.Role)}");
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(string userId, string newPassword)
    {
        // GÜVENLİK: başka bir kullanıcının şifresini sıfırlamak son derece hassas bir işlem - Users.Manage izni yeterli değil, sadece SuperAdmin kullanabilir.
        if (!User.IsInRole(RolePermissions.SuperAdmin))
        {
            return Forbid();
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(newPassword))
        {
            TempData["Error"] = "Yeni şifre boş olamaz.";
            return RedirectToAction(nameof(Edit), new { id = userId });
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, newPassword);
        if (!result.Succeeded)
        {
            TempData["Error"] = string.Join(" ", result.Errors.Select(e => e.Description));
            return RedirectToAction(nameof(Edit), new { id = userId });
        }

        // Şifre değişince mevcut oturumlar (varsa) geçersiz olur - güvenlik amaçlı.
        await _userManager.UpdateSecurityStampAsync(user);

        await _audit.LogAsync(
            _userManager.GetUserId(User),
            User.Identity?.Name,
            "Kullanıcı şifresi sıfırlandı",
            entityName: "ApplicationUser",
            entityId: user.Id,
            details: user.Email);

        TempData["Info"] = $"{user.Email} için şifre sıfırlandı.";
        return RedirectToAction(nameof(Edit), new { id = userId });
    }

    [HttpPost, ActionName("Delete")]
    [Authorize(Policy = Permissions.Users.Delete)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null)
        {
            return NotFound();
        }

        if (string.Equals(_userManager.GetUserId(User), user.Id, StringComparison.Ordinal))
        {
            TempData["Error"] = "Kendi hesabınızı silemezsiniz.";
            return RedirectToAction(nameof(Index));
        }

        // GÜVENLİK: SuperAdmin hesabını sadece başka bir SuperAdmin silebilir.
        var targetRoles = await _userManager.GetRolesAsync(user);
        if (targetRoles.Contains(RolePermissions.SuperAdmin))
        {
            if (!User.IsInRole(RolePermissions.SuperAdmin))
            {
                return Forbid();
            }

            // Sistemdeki son SuperAdmin silinirse kimse admin paneline erişemez hale gelir - buna izin verilmez.
            var superAdminCount = (await _userManager.GetUsersInRoleAsync(RolePermissions.SuperAdmin)).Count;
            if (superAdminCount <= 1)
            {
                TempData["Error"] = "Sistemdeki tek Süper Admin hesabı silinemez.";
                return RedirectToAction(nameof(Index));
            }
        }

        var email = user.Email;
        var result = await _userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            TempData["Error"] = string.Join(" ", result.Errors.Select(e => e.Description));
            return RedirectToAction(nameof(Index));
        }

        await _audit.LogAsync(
            _userManager.GetUserId(User),
            User.Identity?.Name,
            "Kullanıcı silindi",
            entityName: "ApplicationUser",
            entityId: id,
            details: email);

        TempData["Info"] = $"{email} silindi.";
        return RedirectToAction(nameof(Index));
    }
}
