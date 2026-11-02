using System.Text.RegularExpressions;
using RestoranYonetim.Data;

namespace RestoranYonetim.Tests;

// İngilizce site Türkçe metin göstermesin: demo çevirileri ve yasal taslaklar Türkçe karakter içermez (restoran adı hariç).
public class EnglishContentTests
{
    private static readonly Regex TurkishLetters = new("[çğışöüÇĞİŞÖÜ]");

    [Fact]
    public void Demo_Translations_Are_Plain_English()
    {
        var offenders = DemoTranslations.En.Values.Select(v => v.Replace("Örnek Ocakbaşı", ""))
            .Where(v => TurkishLetters.IsMatch(v)).ToList();
        Assert.Empty(offenders);
    }

    [Fact]
    public void Every_Legal_Template_Has_English_Draft_With_Same_Placeholders()
    {
        foreach (var (slug, _, body) in LegalPageTemplates.All)
        {
            Assert.True(LegalPageTemplates.English.TryGetValue(slug, out var english), slug);
            var placeholders = new Regex(@"\{[A-Za-z]+\}");
            Assert.Equal(placeholders.Matches(body).Select(m => m.Value).ToHashSet(), placeholders.Matches(english.Body).Select(m => m.Value).ToHashSet());
            Assert.False(TurkishLetters.IsMatch(english.Body.Replace("KVKK", "")), slug);
            Assert.DoesNotContain("](/kvkk)", english.Body);
        }
    }

    [Fact]
    public void Default_Faq_And_Home_Texts_Are_Covered()
    {
        foreach (var text in new[] { "Rezervasyon nasıl yapabilirim?", "Taze Malzemeler", "Mutlu Misafir", "Genel", "KVKK Aydınlatma Metni" })
        {
            Assert.True(DemoTranslations.En.ContainsKey(text), text);
        }
    }
}
