using Microsoft.EntityFrameworkCore;

namespace RestoranYonetim.Services;

// Masa başına SQL Server uygulama kilidi; çağıranın transaction'ı içinde alınır, commit/rollback ile bırakılır.
// Birden fazla masa kilitlenecekse küçük kimlikten başlanır ki iki işlem birbirini beklemesin.
public static class TableLock
{
    public static async Task AcquireAsync(DbContext context, int tableId)
    {
        var resource = $"adisyon-masa-{tableId}";
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"DECLARE @r int; EXEC @r = sp_getapplock @Resource = {resource}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000; IF @r < 0 THROW 51000, 'Masa kilidi alınamadı.', 1;");
    }

    public static async Task AcquireAsync(DbContext context, int firstTableId, int secondTableId)
    {
        await AcquireAsync(context, Math.Min(firstTableId, secondTableId));
        if (firstTableId != secondTableId)
        {
            await AcquireAsync(context, Math.Max(firstTableId, secondTableId));
        }
    }
}
