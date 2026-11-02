using Microsoft.AspNetCore.DataProtection;
using RestoranYonetim.Services.Email;

namespace RestoranYonetim.Tests;

public class EmailTests
{
    [Fact]
    public void Reservation_Approval_Mail_Escapes_Customer_Input()
    {
        var mail = EmailTemplates.ReservationDecision("<b>Ali</b>", new DateTime(2026, 11, 7), new TimeSpan(19, 30, 0), 4, "Örnek Ocakbaşı", "0212 555 00 00", approved: true);
        Assert.Contains("onaylandı", mail.Subject);
        Assert.Contains("&lt;b&gt;Ali&lt;/b&gt;", mail.Html);
        Assert.DoesNotContain("<b>Ali</b>", mail.Html);
        Assert.Contains("07.11.2026", mail.Html);
        Assert.Contains("19:30", mail.Html);
        Assert.Contains("4 kişi", mail.Html);
    }

    [Fact]
    public void Reservation_Rejection_Mail_Says_So()
    {
        var mail = EmailTemplates.ReservationDecision("Ayşe", new DateTime(2026, 11, 7), new TimeSpan(20, 0, 0), 2, "Örnek Ocakbaşı", null, approved: false);
        Assert.Contains("onaylanamadı", mail.Subject);
        Assert.DoesNotContain("null", mail.Html);
    }

    [Fact]
    public void Password_Reset_Mail_Contains_Encoded_Link()
    {
        var mail = EmailTemplates.PasswordReset("https://site.test/sifre-sifirla?userId=1&token=a+b", "Mizan");
        Assert.Contains("https://site.test/sifre-sifirla?userId=1&amp;token=a+b", mail.Html);
        Assert.Contains("Şifre", mail.Subject);
    }

    [Theory]
    [InlineData("smtp.test", 587, "info@r.test", true)]
    [InlineData("", 587, "info@r.test", false)]
    [InlineData("smtp.test", 587, "", false)]
    [InlineData("smtp.test", 0, "info@r.test", false)]
    [InlineData("smtp.test", 70000, "info@r.test", false)]
    public void Smtp_Settings_Need_Host_Port_And_Sender(string host, int port, string from, bool complete)
    {
        Assert.Equal(complete, new SmtpSettings(host, port, true, null, null, from, null).IsComplete);
    }

    [Fact]
    public void Smtp_Password_Is_Encrypted_And_Readable_Back()
    {
        var protector = new SmtpSecretProtector(new EphemeralDataProtectionProvider());
        var stored = protector.Protect("gizli-sifre");
        Assert.NotEqual("gizli-sifre", stored);
        Assert.Equal("gizli-sifre", protector.Unprotect(stored));
        Assert.Null(protector.Unprotect("bozuk-deger"));
        Assert.Null(protector.Unprotect(null));
    }
}
