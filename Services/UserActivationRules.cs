namespace RestoranYonetim.Services;

// Personel hesabını pasife alma: Identity'nin süresiz kilidi kullanılır (LockoutEnd = MaxValue). Geçici (5 dk) kilitten ayırt etmek için eşik 10 yıl.
public static class UserActivationRules
{
    public static bool IsDeactivated(DateTimeOffset? lockoutEnd) =>
        lockoutEnd.HasValue && lockoutEnd.Value > DateTimeOffset.UtcNow.AddYears(10);

    public static string? BlockReason(bool isSelf, bool targetIsSuperAdmin, bool actorIsSuperAdmin, bool deactivating, int activeSuperAdminCount)
    {
        if (isSelf)
        {
            return "Kendi hesabınızı pasife alamazsınız.";
        }
        if (targetIsSuperAdmin && !actorIsSuperAdmin)
        {
            return "Süper Admin hesaplarını yalnızca bir Süper Admin değiştirebilir.";
        }
        return deactivating && targetIsSuperAdmin && activeSuperAdminCount <= 1
            ? "Sistemdeki son aktif Süper Admin pasife alınamaz."
            : null;
    }
}
