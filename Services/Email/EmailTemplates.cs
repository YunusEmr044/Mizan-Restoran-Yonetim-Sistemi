using System.Globalization;
using System.Net;

namespace RestoranYonetim.Services.Email;

public sealed record EmailMessage(string Subject, string Html);

// Müşteriye ve personele giden e-postalar; kullanıcıdan gelen her metin HTML olarak kaçırılır.
public static class EmailTemplates
{
    private static readonly CultureInfo Tr = CultureInfo.GetCultureInfo("tr-TR");

    private static string E(string? value) => WebUtility.HtmlEncode(value ?? string.Empty);

    private static string Layout(string title, string body, string footer) =>
        $"""
        <div style="font-family:Arial,Helvetica,sans-serif;max-width:560px;margin:0 auto;color:#2b2420;line-height:1.5">
          <h2 style="margin:0 0 16px">{E(title)}</h2>
          {body}
          <p style="margin-top:24px;font-size:12px;color:#8c8275">{E(footer)}</p>
        </div>
        """;

    public static EmailMessage ReservationDecision(string customerName, DateTime date, TimeSpan time, int partySize, string restaurantName, string? restaurantPhone, bool approved)
    {
        var when = $"{date.ToString("dd.MM.yyyy", Tr)} {time.ToString(@"hh\:mm", Tr)} · {partySize} kişi";
        var subject = approved ? $"Rezervasyonunuz onaylandı · {restaurantName}" : $"Rezervasyonunuz onaylanamadı · {restaurantName}";
        var message = approved
            ? "Rezervasyonunuz onaylandı. Sizi ağırlamayı dört gözle bekliyoruz."
            : "Üzgünüz, bu tarih ve saat için rezervasyonunuzu onaylayamadık. Başka bir saat için bizi arayabilir ya da sitemizden yeniden talep gönderebilirsiniz.";
        var phone = string.IsNullOrWhiteSpace(restaurantPhone) ? string.Empty : $"<p>Değişiklik için: <strong>{E(restaurantPhone)}</strong></p>";
        var body = $"<p>Merhaba {E(customerName)},</p><p>{E(message)}</p><p style=\"padding:12px 16px;background:#f5efe6;border-radius:8px\"><strong>{E(when)}</strong></p>{phone}";
        return new EmailMessage(subject, Layout(restaurantName, body, $"Bu e-posta {restaurantName} rezervasyon talebiniz üzerine gönderildi."));
    }

    public static EmailMessage Test(string productName) =>
        new($"Deneme e-postası · {productName}", Layout("Deneme e-postası", "<p>E-posta ayarlarınız çalışıyor. Rezervasyon bildirimleri ve şifre sıfırlama e-postaları bu adresten gönderilecek.</p>", productName));

    public static EmailMessage PasswordReset(string link, string productName)
    {
        var body = $"<p>Hesabınız için şifre sıfırlama istendi. Yeni şifre belirlemek için aşağıdaki bağlantıyı açın:</p>" +
                   $"<p><a href=\"{E(link)}\" style=\"display:inline-block;padding:10px 18px;background:#b8843c;color:#fff;border-radius:8px;text-decoration:none\">Şifremi sıfırla</a></p>" +
                   "<p>Bu isteği siz yapmadıysanız e-postayı yok sayın; şifreniz değişmez.</p>";
        return new EmailMessage($"Şifre sıfırlama · {productName}", Layout("Şifre sıfırlama", body, $"{productName} personel hesabı"));
    }
}
