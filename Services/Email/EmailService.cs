using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Caching.Memory;
using RestoranYonetim.Data;
using RestoranYonetim.Models;

namespace RestoranYonetim.Services.Email;

public sealed record EmailResult(bool Sent, string? Error = null);

// SMTP ile e-posta gönderir; ayar yoksa ya da gönderim başarısızsa istisna fırlatmaz, sonucu döndürür.
public sealed class EmailService
{
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    private readonly ApplicationDbContext _context;
    private readonly IMemoryCache _cache;
    private readonly SmtpSecretProtector _secrets;
    private readonly ILogger<EmailService> _logger;

    public EmailService(ApplicationDbContext context, IMemoryCache cache, SmtpSecretProtector secrets, ILogger<EmailService> logger)
    {
        _context = context;
        _cache = cache;
        _secrets = secrets;
        _logger = logger;
    }

    public SmtpSettings SettingsFrom(SiteContent? content) => new(
        content?.SmtpHost, content?.SmtpPort ?? 587, content?.SmtpUseSsl ?? true, content?.SmtpUserName,
        _secrets.Unprotect(content?.SmtpPasswordProtected), content?.MailFromAddress, content?.MailFromName);

    public async Task<SmtpSettings> SettingsAsync() => SettingsFrom(await SiteContentCache.GetAsync(_context, _cache));

    public async Task<bool> IsConfiguredAsync() => (await SettingsAsync()).IsComplete;

    public async Task<EmailResult> SendAsync(string to, EmailMessage message)
    {
        var settings = await SettingsAsync();
        if (!settings.IsComplete)
        {
            return new EmailResult(false, "E-posta (SMTP) ayarları yapılmamış.");
        }
        try
        {
            using var mail = new MailMessage
            {
                From = new MailAddress(settings.FromAddress!, settings.FromName),
                Subject = message.Subject,
                Body = message.Html,
                IsBodyHtml = true
            };
            mail.To.Add(new MailAddress(to));
            using var client = new SmtpClient(settings.Host, settings.Port)
            {
                EnableSsl = settings.UseSsl,
                Timeout = (int)Timeout.TotalMilliseconds,
                Credentials = string.IsNullOrWhiteSpace(settings.UserName) ? null : new NetworkCredential(settings.UserName, settings.Password)
            };
            using var cts = new CancellationTokenSource(Timeout);
            await client.SendMailAsync(mail, cts.Token);
            return new EmailResult(true);
        }
        catch (Exception ex) when (ex is SmtpException or FormatException or InvalidOperationException or OperationCanceledException or IOException)
        {
            _logger.LogWarning(ex, "E-posta gönderilemedi: {Subject}", message.Subject);
            return new EmailResult(false, ex is FormatException ? "E-posta adresi geçersiz." : "E-posta sunucusuna bağlanılamadı ya da gönderim reddedildi.");
        }
    }
}
