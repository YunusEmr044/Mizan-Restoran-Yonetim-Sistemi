using Microsoft.EntityFrameworkCore;
using RestoranYonetim.Data;
using RestoranYonetim.Models;

namespace RestoranYonetim.Services;

// Masanın aktif adisyonunu bulur/oluşturur; QR ve garson akışlarındaki siparişlerin aynı adisyonda birleşmesini garanti eder.
public class AdisyonService
{
    private readonly ApplicationDbContext _context;
    private readonly InventoryService _inventoryService;
    private readonly RealtimeNotifier _realtime;

    public AdisyonService(ApplicationDbContext context, InventoryService inventoryService, RealtimeNotifier realtime)
    {
        _context = context;
        _inventoryService = inventoryService;
        _realtime = realtime;
    }

    public async Task<Adisyon> GetOrCreateActiveAsync(int tableId)
    {
        var acikDurumlar = new[] { AdisyonStatus.Acik, AdisyonStatus.HesapIstendi, AdisyonStatus.Kasada, AdisyonStatus.KismiOdendi };

        var adisyon = await _context.Adisyonlar
            .Where(a => a.TableId == tableId && acikDurumlar.Contains(a.Status))
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync();

        if (adisyon == null)
        {
            adisyon = new Adisyon { TableId = tableId, Status = AdisyonStatus.Acik };
            _context.Adisyonlar.Add(adisyon);
            await _context.SaveChangesAsync();
        }

        return adisyon;
    }

    // ÖNEMLİ: Tüm adımlar tek transaction içinde yapılır; EnableRetryOnFailure açık olduğundan
    // manuel transaction CreateExecutionStrategy() sarmalayıcısı ile başlatılmak ZORUNDA, aksi halde EF Core hata fırlatır.
    // Çift gönderimi (TOCTOU) önlemek için `IsDraft=true` -> `false` ataması atomik ExecuteUpdateAsync ile yapılır;
    // 0 satır etkilenirse sipariş zaten gönderilmiş demektir, sessizce false döner.
    public async Task<bool> SendOrderAsync(Order order)
    {
        var tableId = order.TableId;
        var orderId = order.Id;
        var tableName = "Masa";
        var strategy = _context.Database.CreateExecutionStrategy();
        var claimedByThisCall = false;

        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var claimed = await _context.Orders
                    .Where(o => o.Id == orderId && o.IsDraft)
                    .ExecuteUpdateAsync(s => s.SetProperty(o => o.IsDraft, false));
                if (claimed == 0)
                {
                    // Sipariş başka bir istek tarafından zaten gönderilmiş; stok tekrar düşmesin.
                    await transaction.CommitAsync();
                    return;
                }
                claimedByThisCall = true;

                var adisyon = await GetOrCreateActiveAsync(tableId);

                order.AdisyonId = adisyon.Id;
                order.IsDraft = false;
                order.SentAt = DateTime.Now;
                foreach (var item in order.Items)
                {
                    item.Status = OrderStatus.Yeni;
                }
                OrderStatusHelper.Recompute(order);

                var table = await _context.RestaurantTables.FindAsync(tableId);
                if (table != null)
                {
                    tableName = table.Name;
                    if (table.Status == TableStatus.Bos)
                    {
                        table.Status = TableStatus.Dolu;
                    }
                }

                await _inventoryService.DeductForOrderAsync(order);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        });

        if (!claimedByThisCall)
        {
            return false;
        }

        // Bildirim bilerek transaction dışında: retry ile tekrar tetiklenmesin ve bildirim
        // hatası sipariş kaydını rollback etmesin (best-effort).
        await _realtime.NotifyManyAsync(new[] { "mutfak", "bar" }, "new-order", $"{tableName}: yeni sipariş");
        return true;
    }
}
