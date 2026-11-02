using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace RestoranYonetim.Services.Email;

public sealed record SmtpSettings(string? Host, int Port, bool UseSsl, string? UserName, string? Password, string? FromAddress, string? FromName)
{
    public bool IsComplete => !string.IsNullOrWhiteSpace(Host) && Port is > 0 and <= 65535 && !string.IsNullOrWhiteSpace(FromAddress);
}

// SMTP şifresi veritabanında Data Protection ile şifreli durur; panel formuna hiç yazılmaz.
public sealed class SmtpSecretProtector
{
    private readonly IDataProtector _protector;

    public SmtpSecretProtector(IDataProtectionProvider provider) => _protector = provider.CreateProtector("Mizan.SmtpPassword.v1");

    public string Protect(string value) => _protector.Protect(value);

    public string? Unprotect(string? stored)
    {
        if (string.IsNullOrEmpty(stored))
        {
            return null;
        }
        try
        {
            return _protector.Unprotect(stored);
        }
        catch (CryptographicException)
        {
            return null;
        }
    }
}
