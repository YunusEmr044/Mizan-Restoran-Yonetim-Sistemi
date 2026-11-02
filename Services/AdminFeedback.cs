using Microsoft.Data.SqlClient;

namespace RestoranYonetim.Services;

// Admin panelinde mesaj bırakmayan işlemler için varsayılan geri bildirim metinleri.
public static class AdminFeedback
{
    private const StringComparison Ordinal = StringComparison.Ordinal;

    public static string SuccessMessageFor(string actionName)
    {
        if (actionName.Contains("Delete", Ordinal) || actionName.StartsWith("Remove", Ordinal))
        {
            return "Silindi.";
        }
        if (actionName.Contains("Create", Ordinal) || actionName.StartsWith("Add", Ordinal))
        {
            return "Eklendi.";
        }
        if (actionName.StartsWith("Toggle", Ordinal) || actionName.StartsWith("Set", Ordinal)
            || actionName.StartsWith("Update", Ordinal) || actionName.StartsWith("Renew", Ordinal))
        {
            return "Güncellendi.";
        }
        return "Kaydedildi.";
    }

    public static string ForeignKeyMessage(string actionName) =>
        actionName.Contains("Delete", Ordinal)
            ? "Bu kayıt geçmiş siparişlerde, stok hareketlerinde ya da başka kayıtlarda kullanıldığı için silinemez."
            : "Bağlı olduğu kayıt bulunamadığı için kaydedilemedi. Sayfayı yenileyip tekrar deneyin.";

    public static bool IsForeignKeyViolation(Exception exception)
    {
        for (var e = exception; e != null; e = e.InnerException)
        {
            if (e is SqlException { Number: 547 })
            {
                return true;
            }
        }
        return false;
    }
}
