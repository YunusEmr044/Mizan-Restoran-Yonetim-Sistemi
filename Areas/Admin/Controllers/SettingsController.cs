using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RestoranYonetim.Data;
using RestoranYonetim.Models;
using RestoranYonetim.Security;
using RestoranYonetim.Services;
using RestoranYonetim.Services.Email;
using RestoranYonetim.Services.Publishing;

namespace RestoranYonetim.Areas.Admin.Controllers;

// Sadece Süper Admin'in erişebildiği genel sistem ayarları ekranı: görünüm/tema. İçerik tekil SiteContent kaydında tutulur (Site İçeriği ekranıyla aynı kayıt).
[Area("Admin")]
[Authorize(Roles = RolePermissions.SuperAdmin)]
public class SettingsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly AuditService _audit;
    private readonly IConfiguration _configuration;
    private readonly EmailService _email;
    private readonly SmtpSecretProtector _secrets;

    public SettingsController(ApplicationDbContext context, IMemoryCache cache, AuditService audit, IConfiguration configuration, EmailService email, SmtpSecretProtector secrets)
    {
        _context = context;
        _cache = cache;
        _audit = audit;
        _configuration = configuration;
        _email = email;
        _secrets = secrets;
    }

    private async Task<SiteContent> GetOrCreateAsync()
    {
        var content = await _context.SiteContents.FirstOrDefaultAsync();
        if (content == null)
        {
            content = new SiteContent();
            _context.SiteContents.Add(content);
            await _context.SaveChangesAsync();
        }
        return content;
    }

    public async Task<IActionResult> Index()
    {
        var content = await GetOrCreateAsync();
        await PopulatePublicUrlInfoAsync();
        return View(content);
    }

    private async Task PopulatePublicUrlInfoAsync()
    {
        var (url, source) = await PublicUrl.ResolveWithSourceAsync(HttpContext, _context, _cache, _configuration);
        ViewBag.PublicUrlCurrent = url;
        ViewBag.PublicUrlSource = source;

        // Uygulamanın dinlediği port (ör. 5080) önerilen adreslere eklenir.
        var port = HttpContext.Connection.LocalPort;
        var portSuffix = port is 0 or 80 ? string.Empty : $":{port}";
        ViewBag.PublicUrlSuggestions = PublicUrl.GetLocalNetworkAddresses()
            .Select(a => (Url: $"http://{a.Ip}{portSuffix}", a.InterfaceName, a.HasGateway))
            .ToList();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SavePublicBaseUrl(string? publicBaseUrl)
    {
        string? newValue = null;
        if (!string.IsNullOrWhiteSpace(publicBaseUrl))
        {
            if (!PublicUrl.TryNormalize(publicBaseUrl, out var normalized) || normalized.Length > 200)
            {
                TempData["Error"] = "Geçerli bir adres girin: http:// veya https:// ile başlamalı, ör. http://192.168.1.50:5080 ya da https://restoraniniz.com";
                return RedirectToAction(nameof(Index));
            }
            newValue = normalized;
        }

        var content = await GetOrCreateAsync();
        var oldValue = content.PublicBaseUrl;
        content.PublicBaseUrl = newValue;
        await _context.SaveChangesAsync();
        SiteContentCache.Invalidate(_cache);

        // QR kodlarının yönlendiği adres değişiyor - masalardaki basılı QR'ları etkileyen kritik bir ayar.
        await _audit.LogAsync(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            User.Identity?.Name,
            "Site adresi güncellendi",
            entityName: "SiteContent",
            entityId: content.Id.ToString(),
            details: $"{oldValue ?? "(boş)"} → {newValue ?? "(boş - ayar dosyası kullanılır)"}");

        TempData["Info"] = newValue == null
            ? "Site adresi temizlendi; ayar dosyasındaki adres kullanılacak."
            : $"Site adresi kaydedildi: {newValue}";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveEmail(string? smtpHost, int smtpPort, bool smtpUseSsl, string? smtpUserName, string? smtpPassword,
        bool clearPassword, string? mailFromAddress, string? mailFromName)
    {
        static string? Clean(string? value, int max) => string.IsNullOrWhiteSpace(value) ? null : value.Trim()[..Math.Min(value.Trim().Length, max)];
        var host = Clean(smtpHost, 200);
        var from = Clean(mailFromAddress, 200);
        if (host != null && smtpPort is < 1 or > 65535)
        {
            TempData["Error"] = "Port 1 ile 65535 arasında olmalı (genellikle 587 ya da 465).";
            return RedirectToAction(nameof(Index));
        }
        if (host != null && (from == null || !System.Net.Mail.MailAddress.TryCreate(from, out _)))
        {
            TempData["Error"] = "Gönderen e-posta adresini geçerli bir adres olarak girin.";
            return RedirectToAction(nameof(Index));
        }

        var content = await GetOrCreateAsync();
        content.SmtpHost = host;
        content.SmtpPort = smtpPort is >= 1 and <= 65535 ? smtpPort : 587;
        content.SmtpUseSsl = smtpUseSsl;
        content.SmtpUserName = Clean(smtpUserName, 200);
        content.MailFromAddress = from;
        content.MailFromName = Clean(mailFromName, 100);
        if (clearPassword)
        {
            content.SmtpPasswordProtected = null;
        }
        else if (!string.IsNullOrEmpty(smtpPassword))
        {
            content.SmtpPasswordProtected = _secrets.Protect(smtpPassword);
        }
        await _context.SaveChangesAsync();
        SiteContentCache.Invalidate(_cache);
        await _audit.LogAsync(User.FindFirstValue(ClaimTypes.NameIdentifier), User.Identity?.Name, "E-posta ayarları güncellendi",
            entityName: "SiteContent", entityId: content.Id.ToString(), details: host == null ? "kapalı" : $"{host}:{content.SmtpPort} · {from}");
        TempData["Info"] = host == null ? "E-posta gönderimi kapatıldı." : "E-posta ayarları kaydedildi. Deneme e-postası göndererek kontrol edin.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendTestEmail(string? testTo)
    {
        var to = string.IsNullOrWhiteSpace(testTo) ? User.FindFirstValue(ClaimTypes.Email) ?? User.Identity?.Name : testTo.Trim();
        if (string.IsNullOrWhiteSpace(to) || !System.Net.Mail.MailAddress.TryCreate(to, out _))
        {
            TempData["Error"] = "Deneme için geçerli bir e-posta adresi girin.";
            return RedirectToAction(nameof(Index));
        }
        var result = await _email.SendAsync(to, EmailTemplates.Test(ProductInfo.Name));
        TempData[result.Sent ? "Info" : "Error"] = result.Sent ? $"Deneme e-postası gönderildi: {to}" : $"Deneme e-postası gönderilemedi: {result.Error}";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveAnalytics(AnalyticsProvider analyticsProvider, string? analyticsId, string? analyticsScriptUrl)
    {
        var error = AnalyticsSettings.Validate(analyticsProvider, analyticsId, analyticsScriptUrl);
        if (error != null)
        {
            TempData["Error"] = error;
            return RedirectToAction(nameof(Index));
        }

        var content = await GetOrCreateAsync();
        content.AnalyticsProvider = analyticsProvider;
        content.AnalyticsId = analyticsProvider == AnalyticsProvider.Yok ? null : analyticsId?.Trim();
        content.AnalyticsScriptUrl = analyticsProvider == AnalyticsProvider.Umami ? analyticsScriptUrl?.Trim() : null;
        await _context.SaveChangesAsync();
        SiteContentCache.Invalidate(_cache);

        await _audit.LogAsync(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            User.Identity?.Name,
            "Analytics ayarı güncellendi",
            entityName: "SiteContent",
            entityId: content.Id.ToString(),
            details: $"{analyticsProvider} {content.AnalyticsId}");

        TempData["Info"] = analyticsProvider == AnalyticsProvider.Yok
            ? "Analytics kapatıldı."
            : "Analytics ayarı kaydedildi.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveReceipt(ReceiptSettingsInput input)
    {
        var content = await GetOrCreateAsync();
        content.ReceiptPaperSize = ReceiptLayout.SafePaper(input.ReceiptPaperSize);
        content.ReceiptFontSize = ReceiptLayout.SafeFontSize(input.ReceiptFontSize);
        content.ReceiptShowAddress = input.ReceiptShowAddress;
        content.ReceiptShowPhone = input.ReceiptShowPhone;
        content.ReceiptShowWaiter = input.ReceiptShowWaiter;
        content.ReceiptShowPayments = input.ReceiptShowPayments;
        content.ReceiptTaxInfo = Trimmed(input.ReceiptTaxInfo, 200);
        content.ReceiptFooterText = Trimmed(input.ReceiptFooterText, 300);
        await _context.SaveChangesAsync();
        SiteContentCache.Invalidate(_cache);

        await _audit.LogAsync(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            User.Identity?.Name,
            "Adisyon çıktısı ayarları güncellendi",
            entityName: "SiteContent",
            entityId: content.Id.ToString(),
            details: $"{content.ReceiptPaperSize} {content.ReceiptFontSize}");

        TempData["Info"] = "Adisyon çıktısı ayarları kaydedildi.";
        return RedirectToAction(nameof(Index));
    }

    private static string? Trimmed(string? value, int maxLength)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrEmpty(trimmed)) return null;
        return trimmed.Length > maxLength ? trimmed[..maxLength] : trimmed;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(ThemeSettingsInput input)
    {
        var content = await GetOrCreateAsync();

        content.ThemePrimaryColor = ThemeCatalog.SafeColor(input.ThemePrimaryColor, ThemeCatalog.DefaultPrimary);
        content.ThemeDarkColor = ThemeCatalog.SafeColor(input.ThemeDarkColor, ThemeCatalog.DefaultDark);
        content.ThemeBackgroundColor = ThemeCatalog.SafeColor(input.ThemeBackgroundColor, ThemeCatalog.DefaultBackground);
        content.ThemeTextColor = ThemeCatalog.SafeColor(input.ThemeTextColor, ThemeCatalog.DefaultText);
        content.ThemeHeadingFont = ThemeCatalog.SafeHeadingFont(input.ThemeHeadingFont);
        content.ThemeBodyFont = ThemeCatalog.SafeBodyFont(input.ThemeBodyFont);
        content.ThemeCornerStyle = ThemeCatalog.SafeCornerStyle(input.ThemeCornerStyle);

        content.AdminAccentColor = ThemeCatalog.SafeColor(input.AdminAccentColor, ThemeCatalog.DefaultAdminAccent);
        content.AdminSidebarColor = ThemeCatalog.SafeColor(input.AdminSidebarColor, ThemeCatalog.DefaultAdminSidebar);
        content.AdminBackgroundColor = ThemeCatalog.SafeColor(input.AdminBackgroundColor, ThemeCatalog.DefaultAdminBackground);

        await _context.SaveChangesAsync();
        SiteContentCache.Invalidate(_cache);

        // Sistem geneli tema değişikliği tüm public site + admin paneli etkiler - loglanır.
        await _audit.LogAsync(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            User.Identity?.Name,
            "Görünüm/tema ayarları güncellendi",
            entityName: "SiteContent",
            entityId: content.Id.ToString());

        TempData["Info"] = "Görünüm ayarları güncellendi.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reset()
    {
        var content = await GetOrCreateAsync();

        content.ThemePrimaryColor = ThemeCatalog.DefaultPrimary;
        content.ThemeDarkColor = ThemeCatalog.DefaultDark;
        content.ThemeBackgroundColor = ThemeCatalog.DefaultBackground;
        content.ThemeTextColor = ThemeCatalog.DefaultText;
        content.ThemeHeadingFont = ThemeCatalog.DefaultHeadingFont;
        content.ThemeBodyFont = ThemeCatalog.DefaultBodyFont;
        content.ThemeCornerStyle = ThemeCatalog.DefaultCornerStyle;
        content.AdminAccentColor = ThemeCatalog.DefaultAdminAccent;
        content.AdminSidebarColor = ThemeCatalog.DefaultAdminSidebar;
        content.AdminBackgroundColor = ThemeCatalog.DefaultAdminBackground;

        await _context.SaveChangesAsync();
        SiteContentCache.Invalidate(_cache);

        await _audit.LogAsync(
            User.FindFirstValue(ClaimTypes.NameIdentifier),
            User.Identity?.Name,
            "Görünüm/tema ayarları varsayılana döndürüldü",
            entityName: "SiteContent",
            entityId: content.Id.ToString());

        TempData["Info"] = "Görünüm ayarları varsayılana döndürüldü.";
        return RedirectToAction(nameof(Index));
    }
}

// SiteContent'in diğer alanlarını yanlışlıkla boşaltmamak için ayrı, dar bir input modeli kullanılır.
public class ThemeSettingsInput
{
    public string? ThemePrimaryColor { get; set; }
    public string? ThemeDarkColor { get; set; }
    public string? ThemeBackgroundColor { get; set; }
    public string? ThemeTextColor { get; set; }
    public string? ThemeHeadingFont { get; set; }
    public string? ThemeBodyFont { get; set; }
    public string? ThemeCornerStyle { get; set; }
    public string? AdminAccentColor { get; set; }
    public string? AdminSidebarColor { get; set; }
    public string? AdminBackgroundColor { get; set; }
}

public class ReceiptSettingsInput
{
    public string? ReceiptPaperSize { get; set; }
    public string? ReceiptFontSize { get; set; }
    public bool ReceiptShowAddress { get; set; }
    public bool ReceiptShowPhone { get; set; }
    public bool ReceiptShowWaiter { get; set; }
    public bool ReceiptShowPayments { get; set; }
    public string? ReceiptTaxInfo { get; set; }
    public string? ReceiptFooterText { get; set; }
}
