using Microsoft.AspNetCore.Identity;

namespace RestoranYonetim.Security;

// Şifre kuralları ve hesap hataları kullanıcıya Türkçe gösterilir (çerçevenin İngilizce varsayılanları yerine).
public sealed class TurkishIdentityErrorDescriber : IdentityErrorDescriber
{
    private static IdentityError E(string code, string description) => new() { Code = code, Description = description };

    public override IdentityError DefaultError() => E(nameof(DefaultError), "Beklenmeyen bir hata oluştu.");
    public override IdentityError ConcurrencyFailure() => E(nameof(ConcurrencyFailure), "Kayıt başka biri tarafından değiştirildi; sayfayı yenileyip tekrar deneyin.");
    public override IdentityError PasswordMismatch() => E(nameof(PasswordMismatch), "Şifre hatalı.");
    public override IdentityError InvalidToken() => E(nameof(InvalidToken), "Bağlantı geçersiz ya da süresi dolmuş; yeni bir bağlantı isteyin.");
    public override IdentityError LoginAlreadyAssociated() => E(nameof(LoginAlreadyAssociated), "Bu giriş zaten başka bir hesaba bağlı.");
    public override IdentityError InvalidUserName(string? userName) => E(nameof(InvalidUserName), $"Kullanıcı adı geçersiz: {userName}");
    public override IdentityError InvalidEmail(string? email) => E(nameof(InvalidEmail), $"E-posta adresi geçersiz: {email}");
    public override IdentityError DuplicateUserName(string userName) => E(nameof(DuplicateUserName), $"{userName} kullanıcı adı zaten kullanılıyor.");
    public override IdentityError DuplicateEmail(string email) => E(nameof(DuplicateEmail), $"{email} adresi zaten kullanılıyor.");
    public override IdentityError InvalidRoleName(string? role) => E(nameof(InvalidRoleName), $"Rol adı geçersiz: {role}");
    public override IdentityError DuplicateRoleName(string role) => E(nameof(DuplicateRoleName), $"{role} rolü zaten var.");
    public override IdentityError UserAlreadyHasPassword() => E(nameof(UserAlreadyHasPassword), "Kullanıcının zaten bir şifresi var.");
    public override IdentityError UserLockoutNotEnabled() => E(nameof(UserLockoutNotEnabled), "Bu kullanıcı için kilitleme açık değil.");
    public override IdentityError UserAlreadyInRole(string role) => E(nameof(UserAlreadyInRole), $"Kullanıcı zaten {role} rolünde.");
    public override IdentityError UserNotInRole(string role) => E(nameof(UserNotInRole), $"Kullanıcı {role} rolünde değil.");
    public override IdentityError PasswordTooShort(int length) => E(nameof(PasswordTooShort), $"Şifre en az {length} karakter olmalı.");
    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) => E(nameof(PasswordRequiresUniqueChars), $"Şifre en az {uniqueChars} farklı karakter içermeli.");
    public override IdentityError PasswordRequiresNonAlphanumeric() => E(nameof(PasswordRequiresNonAlphanumeric), "Şifre en az bir sembol içermeli.");
    public override IdentityError PasswordRequiresDigit() => E(nameof(PasswordRequiresDigit), "Şifre en az bir rakam içermeli.");
    public override IdentityError PasswordRequiresLower() => E(nameof(PasswordRequiresLower), "Şifre en az bir küçük harf içermeli.");
    public override IdentityError PasswordRequiresUpper() => E(nameof(PasswordRequiresUpper), "Şifre en az bir büyük harf içermeli.");
    public override IdentityError RecoveryCodeRedemptionFailed() => E(nameof(RecoveryCodeRedemptionFailed), "Kurtarma kodu kullanılamadı.");
}
