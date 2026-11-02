using RestoranYonetim.Security;

namespace RestoranYonetim.Tests;

public class TurkishIdentityErrorsTests
{
    private readonly TurkishIdentityErrorDescriber _d = new();

    [Fact]
    public void Password_Rule_Messages_Are_Turkish()
    {
        Assert.Equal("Şifre en az 8 karakter olmalı.", _d.PasswordTooShort(8).Description);
        Assert.Equal("Şifre en az bir rakam içermeli.", _d.PasswordRequiresDigit().Description);
        Assert.Equal("Şifre en az bir küçük harf içermeli.", _d.PasswordRequiresLower().Description);
    }

    [Fact]
    public void Account_Messages_Are_Turkish()
    {
        Assert.Contains("zaten kullanılıyor", _d.DuplicateEmail("a@b.c").Description);
        Assert.Contains("geçersiz ya da süresi dolmuş", _d.InvalidToken().Description);
        Assert.Equal("PasswordMismatch", _d.PasswordMismatch().Code);
    }
}
