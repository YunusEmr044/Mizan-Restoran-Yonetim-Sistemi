using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RestoranYonetim.Data;
using RestoranYonetim.Models;
using RestoranYonetim.Security;
using Microsoft.Extensions.Caching.Memory;
using RestoranYonetim.Services;
using RestoranYonetim.Services.Email;
using Microsoft.AspNetCore.Authorization;

namespace RestoranYonetim.Controllers;

public class ChangePasswordViewModel
{
    [Required(ErrorMessage = "Mevcut şifre gereklidir.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mevcut şifre")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Yeni şifre gereklidir.")]
    [DataType(DataType.Password)]
    [Display(Name = "Yeni şifre")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Yeni şifreyi tekrar girin.")]
    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "Yeni şifreler eşleşmiyor.")]
    [Display(Name = "Yeni şifre (tekrar)")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class ResetPasswordViewModel
{
    public string UserId { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;

    [Required(ErrorMessage = "Yeni şifre gereklidir.")]
    [DataType(DataType.Password)]
    [Display(Name = "Yeni şifre")]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Yeni şifreyi tekrar girin.")]
    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "Şifreler eşleşmiyor.")]
    [Display(Name = "Yeni şifre (tekrar)")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

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
    private readonly AuditService _audit;
    private readonly EmailService _email;
    private readonly IMemoryCache _cache;
    private readonly IConfiguration _configuration;

    public AccountController(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager, ApplicationDbContext context, ILogger<AccountController> logger, AuditService audit,
        EmailService email, IMemoryCache cache, IConfiguration configuration)
    {
        _email = email;
        _cache = cache;
        _configuration = configuration;
        _audit = audit;
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
            var lockedUser = await _userManager.FindByEmailAsync(model.Email);
            ModelState.AddModelError(string.Empty, lockedUser != null && UserActivationRules.IsDeactivated(lockedUser.LockoutEnd) && await _userManager.CheckPasswordAsync(lockedUser, model.Password)
                ? "Hesabınız pasif durumda. Yöneticinizle görüşün."
                : "Çok sayıda başarısız giriş denemesi nedeniyle bu hesap geçici olarak kilitlendi. Lütfen birkaç dakika sonra tekrar deneyin.");
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

        // Operasyon rolleri ve Roller ekranından eklenen özel roller sahip oldukları izinlere göre erişebildikleri
        // ilk ekrana gider (her aday kendi [Authorize] politikasıyla birebir eşleşir, aksi halde hemen 403 alınırdı).
        var permissions = await PermissionResolver.ResolveAsync(_context, roles, _logger);

        if (permissions.Contains(Permissions.Reports.View))
        {
            return RedirectToAction("Index", "Dashboard", new { area = "Admin" });
        }

        if (RolePermissions.OperationalLanding(permissions) is string landing)
        {
            return Redirect(IsReturnUrlAllowed(returnUrl, landing) ? returnUrl! : landing);
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

    [Authorize]
    [HttpGet("/hesabim/sifre")]
    public IActionResult ChangePassword() => View(new ChangePasswordViewModel());

    [Authorize]
    [HttpPost("/hesabim/sifre")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return RedirectToAction(nameof(Login));
        }

        var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Code == "PasswordMismatch" ? "Mevcut şifre hatalı." : error.Description);
            }
            return View(model);
        }

        await _signInManager.RefreshSignInAsync(user);
        await _audit.LogAsync(user.Id, user.Email, "Şifre değiştirildi", entityName: "ApplicationUser", entityId: user.Id);
        TempData["Info"] = "Şifreniz değiştirildi.";
        return RedirectToAction(nameof(ChangePassword));
    }

    [HttpGet("/sifremi-unuttum")]
    public async Task<IActionResult> ForgotPassword() =>
        await _email.IsConfiguredAsync() ? View() : RedirectToAction(nameof(Login));

    // Adresin kayıtlı olup olmadığı yanıttan anlaşılmaz; pasif hesaplara bağlantı gönderilmez.
    [HttpPost("/sifremi-unuttum")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> ForgotPassword(string? email)
    {
        var user = string.IsNullOrWhiteSpace(email) ? null : await _userManager.FindByEmailAsync(email.Trim());
        if (user?.Email != null && !UserActivationRules.IsDeactivated(user.LockoutEnd) && await _email.IsConfiguredAsync())
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var baseUrl = await PublicUrl.ResolveAsync(HttpContext, _context, _cache, _configuration);
            var link = $"{baseUrl}/sifre-sifirla?userId={Uri.EscapeDataString(user.Id)}&token={Uri.EscapeDataString(token)}";
            var result = await _email.SendAsync(user.Email, EmailTemplates.PasswordReset(link, ProductInfo.Name));
            await _audit.LogAsync(user.Id, user.Email, result.Sent ? "Şifre sıfırlama bağlantısı gönderildi" : "Şifre sıfırlama e-postası gönderilemedi",
                entityName: "ApplicationUser", entityId: user.Id);
        }
        ViewBag.Sent = true;
        return View();
    }

    [HttpGet("/sifre-sifirla")]
    public IActionResult ResetPassword(string? userId, string? token) =>
        string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(token)
            ? RedirectToAction(nameof(ForgotPassword))
            : View(new ResetPasswordViewModel { UserId = userId, Token = token });

    [HttpPost("/sifre-sifirla")]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }
        var user = await _userManager.FindByIdAsync(model.UserId);
        var result = user == null || UserActivationRules.IsDeactivated(user.LockoutEnd)
            ? IdentityResult.Failed(_userManager.ErrorDescriber.InvalidToken())
            : await _userManager.ResetPasswordAsync(user, model.Token, model.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return View(model);
        }
        await _userManager.ResetAccessFailedCountAsync(user!);
        await _audit.LogAsync(user!.Id, user.Email, "Şifre e-posta bağlantısıyla sıfırlandı", entityName: "ApplicationUser", entityId: user.Id);
        TempData["LoginInfo"] = "Şifreniz değiştirildi; yeni şifrenizle giriş yapın.";
        return Redirect("/admin");
    }
}
