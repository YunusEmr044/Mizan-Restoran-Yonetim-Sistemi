using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RestoranYonetim.Data;
using RestoranYonetim.Models;
using RestoranYonetim.Security;

namespace RestoranYonetim.Controllers;

public class LoginViewModel
{
    [Required(ErrorMessage = "E-posta gereklidir.")]
    [Display(Name = "E-posta")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Şifre gereklidir.")]
    [DataType(DataType.Password)]
    [Display(Name = "Şifre")]
    public string Password { get; set; } = string.Empty;

    public string? ReturnUrl { get; set; }

    // Kilitle ile dönüldüğünde e-posta önceden dolu gelir, kullanıcı sadece şifresini yazar.
    public bool Locked { get; set; }
}

// Personel/yönetici girişi - ayrı bir "/giris" sayfası yok, tek adres "/admin": girişli değilse
// bu form gösterilir, girişliyse rolüne göre ilgili ekrana yönlendirilir (bkz. Login(GET) ve RedirectForRoles).
public class AccountController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _context;
    private readonly ILogger<AccountController> _logger;

    public AccountController(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager, ApplicationDbContext context, ILogger<AccountController> logger)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _context = context;
        _logger = logger;
    }

    [HttpGet("/admin")]
    public async Task<IActionResult> Login(string? returnUrl = null, string? email = null)
    {
        // Zaten girişliyse formu tekrar göstermek yerine doğrudan ilgili ekrana gönder.
        if (User.Identity?.IsAuthenticated == true)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                var roles = await _userManager.GetRolesAsync(user);
                var redirect = await RedirectForRolesAsync(roles, returnUrl);
                if (redirect != null)
                {
                    return redirect;
                }
            }

            // Erişilebilir ekranı olmayan bir hesapla girişli kalınmış - "/admin"e sonsuz döngü yerine çıkış yaptır.
            await _signInManager.SignOutAsync();
        }

        return View(new LoginViewModel
        {
            ReturnUrl = returnUrl,
            Email = email ?? string.Empty,
            Locked = !string.IsNullOrEmpty(email)
        });
    }

    [HttpPost("/admin")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // lockoutOnFailure: true - brute-force koruması; ayrıca /admin için Program.cs'te ayrı hız sınırlama var.
        var result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, isPersistent: true, lockoutOnFailure: true);
        if (result.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty, "Çok sayıda başarısız giriş denemesi nedeniyle bu hesap geçici olarak kilitlendi. Lütfen birkaç dakika sonra tekrar deneyin.");
            return View(model);
        }
        if (result.Succeeded)
        {
            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user != null)
            {
                var roles = await _userManager.GetRolesAsync(user);
                var redirect = await RedirectForRolesAsync(roles, model.ReturnUrl);
                if (redirect != null)
                {
                    return redirect;
                }
            }

            await _signInManager.SignOutAsync();
            ModelState.AddModelError(string.Empty, "Bu kullanıcı için erişilebilir bir ekran tanımlı değil.");
            return View(model);
        }

        ModelState.AddModelError(string.Empty, "E-posta veya şifre hatalı.");
        return View(model);
    }

    // Role göre iniş ekranı: hem başarılı giriş sonrası hem de zaten girişli kullanıcı "/admin"e tekrar gelirse kullanılır.
    // null dönerse çağıran taraf kullanıcıyı çıkışa zorlamalı - aksi halde eşleşen rolü olmayan bir hesap "/admin"e sonsuz döngüye girer.
    private async Task<IActionResult?> RedirectForRolesAsync(IList<string> roles, string? returnUrl)
    {
        if (roles.Any(r => RolePermissions.ManagementRoles.Contains(r)))
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index", "Dashboard", new { area = "Admin" });
        }

        if (roles.Contains(RolePermissions.DepoPersoneli))
        {
            if (IsReturnUrlAllowed(returnUrl, "/Admin/Inventory"))
            {
                return Redirect(returnUrl!);
            }

            return Redirect("/Admin/Inventory");
        }

        if (roles.Contains(RolePermissions.Muhasebe) || roles.Contains(RolePermissions.RaporGoruntuleyici))
        {
            if (IsReturnUrlAllowed(returnUrl, "/Admin/Reports"))
            {
                return Redirect(returnUrl!);
            }

            return Redirect("/Admin/Reports");
        }

        if (roles.Any(r => RolePermissions.OperationalRoles.Contains(r)))
        {
            if (IsReturnUrlAllowed(returnUrl, "/Staff"))
            {
                return Redirect(returnUrl!);
            }

            return RedirectToAction("Index", "Staff");
        }

        // Yukarıdakilerin hiçbiri değil - Roller ekranından eklenen özel bir rol olabilir. Bu roller sabit
        // kodlanmış bir iniş ekranına sahip değil, o yüzden sahip olduğu izinlere bakıp erişebileceği ilk
        // ekrana yönlendiriyoruz (her adayın kendi [Authorize] politikasıyla birebir eşleşiyor, aksi halde
        // hemen 403 ile karşılaşırdı).
        var permissions = await PermissionResolver.ResolveAsync(_context, roles, _logger);

        if (permissions.Contains(Permissions.Reports.View))
        {
            return RedirectToAction("Index", "Dashboard", new { area = "Admin" });
        }

        if (permissions.Contains(Permissions.Orders.View))
        {
            return RedirectToAction("Index", "Staff");
        }

        if (permissions.Contains(Permissions.Cash.View))
        {
            return RedirectToAction("Index", "Kasa");
        }

        if (permissions.Contains(Permissions.Inventory.View))
        {
            return Redirect("/Admin/Inventory");
        }

        // Not: UsersController sınıf düzeyinde Users.Manage istiyor - Users.View tek başına oraya erişim vermez.
        if (permissions.Contains(Permissions.Users.Manage))
        {
            return RedirectToAction("Index", "Users", new { area = "Admin" });
        }

        return null;
    }

    private bool IsReturnUrlAllowed(string? returnUrl, string allowedPrefix)
    {
        return !string.IsNullOrEmpty(returnUrl)
            && Url.IsLocalUrl(returnUrl)
            && returnUrl.StartsWith(allowedPrefix, StringComparison.OrdinalIgnoreCase);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    // Kilitle: tam çıkış değil - oturumu kapatır ama e-postayı hatırlar, ekrandan uzaklaşan personelin
    // bilgisayarına başkasının şifre girmeden erişememesi için. "Giriş Yap" ile aynı sayfa, sadece e-posta dolu gelir.
    [HttpPost("/kilitle")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Lock(string? returnUrl)
    {
        var email = User.Identity?.Name;
        await _signInManager.SignOutAsync();

        if (string.IsNullOrEmpty(email))
        {
            return RedirectToAction(nameof(Login));
        }

        return RedirectToAction(nameof(Login), new
        {
            email,
            returnUrl = !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : null
        });
    }
}
