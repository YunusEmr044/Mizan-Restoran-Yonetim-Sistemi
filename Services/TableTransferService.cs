using Microsoft.EntityFrameworkCore;
using RestoranYonetim.Data;
using RestoranYonetim.Extensions;
using RestoranYonetim.Models;

namespace RestoranYonetim.Services;

public sealed record TransferResult(string? Error, int? AdisyonId = null, int? TableId = null);

public sealed record TransferTarget(int Id, string Name, string AreaName, bool Occupied);

// Masa taşıma, birleştirme ve ürün aktarma. İki masa tek transaction içinde, kimlik sırasıyla kilitlenir.
public class TableTransferService
{
    private readonly ApplicationDbContext _context;
    private readonly AuditService _audit;
    private readonly RealtimeNotifier _realtime;

    public TableTransferService(ApplicationDbContext context, AuditService audit, RealtimeNotifier realtime)
    {
        _context = context;
        _audit = audit;
        _realtime = realtime;
    }

    public async Task<IReadOnlyList<TransferTarget>> LoadTargetsAsync(int? exceptTableId)
    {
        var activeTables = await _context.Adisyonlar.AsNoTracking()
            .Where(a => a.TableId != null && Adisyon.ActiveStatuses.Contains(a.Status))
            .Select(a => a.TableId!.Value)
            .ToListAsync();
        return await _context.RestaurantTables.AsNoTracking()
            .Where(t => t.Id != exceptTableId)
            .OrderBy(t => t.Area!.Name).ThenBy(t => t.Id)
            .Select(t => new TransferTarget(t.Id, t.Name, t.Area!.Name, activeTables.Contains(t.Id)))
            .ToListAsync();
    }

    public async Task<TransferResult> MoveAsync(int adisyonId, int targetTableId, string? userId, string? userName)
    {
        TransferResult result = new(null);
        string? sourceName = null, targetName = null;
        var merged = false;

        try
        {
            await _context.ExecuteInTransactionAsync(async () =>
            {
                var sourceTableId = await SourceTableIdAsync(adisyonId);
                if (sourceTableId == null)
                {
                    result = new("Paket siparişler masaya taşınamaz.");
                    return;
                }
                await TableLock.AcquireAsync(_context, sourceTableId.Value, targetTableId);

                var source = await LoadAdisyonAsync(adisyonId);
                var targetTable = await _context.RestaurantTables.FindAsync(targetTableId);
                if (source == null || targetTable == null)
                {
                    result = new("Adisyon ya da hedef masa bulunamadı.");
                    return;
                }
                if (source.TableId != sourceTableId)
                {
                    result = new(MovedMeanwhile);
                    return;
                }

                var target = await ActiveOnTableAsync(targetTableId);
                var reason = TableTransferRules.MoveOrMergeBlockReason(source, targetTableId, target, await HasPendingVoidAsync(source.Id));
                if (reason != null)
                {
                    result = new(reason);
                    return;
                }

                var sourceTable = source.Table!;
                sourceName = sourceTable.Name;
                targetName = targetTable.Name;

                if (target == null)
                {
                    await MoveWholeAsync(source, sourceTable, targetTable);
                    result = new(null, source.Id, targetTableId);
                }
                else
                {
                    var drafts = await DraftsOnTableAsync(sourceTable.Id);
                    foreach (var order in source.Orders)
                    {
                        order.AdisyonId = target.Id;
                        order.TableId = targetTableId;
                    }
                    foreach (var draft in drafts)
                    {
                        draft.TableId = targetTableId;
                    }
                    foreach (var payment in source.Payments)
                    {
                        payment.AdisyonId = target.Id;
                    }
                    foreach (var request in source.DiscountRequests.Where(d => d.Status == DiscountRequestStatus.Beklemede))
                    {
                        request.AdisyonId = target.Id;
                    }
                    if (source.DiscountAmount > 0)
                    {
                        target.DiscountAmount += source.DiscountAmount;
                        target.DiscountReason = string.Join(" / ", new[] { target.DiscountReason, source.DiscountReason }.Where(r => !string.IsNullOrWhiteSpace(r)));
                    }

                    CloseAsMerged(source, target.Id);
                    MarkChanged(target);
                    sourceTable.Status = TableStatus.Bos;
                    merged = true;
                    result = new(null, target.Id, targetTableId);
                }
            });
        }
        catch (DbUpdateConcurrencyException)
        {
            return new(ChangedMeanwhile);
        }

        if (result.Error == null)
        {
            var action = merged ? "Masalar birleştirildi" : "Masa taşındı";
            await _audit.LogAsync(userId, userName, action, entityName: "Adisyon", entityId: adisyonId.ToString(), details: $"{sourceName} → {targetName}");
            await _realtime.NotifyManyAsync(new[] { "mutfak", "bar", "staff" }, "table-transfer", $"{sourceName} → {targetName}");
        }
        return result;
    }

    public async Task<TransferResult> TransferItemsAsync(int adisyonId, int targetTableId, IReadOnlyDictionary<int, int> quantities, string? userId, string? userName)
    {
        TransferResult result = new(null);
        string? sourceName = null, targetName = null;
        var selected = quantities.Where(q => q.Value != 0).ToDictionary(q => q.Key, q => q.Value);

        try
        {
            await _context.ExecuteInTransactionAsync(async () =>
            {
                var sourceTableId = await SourceTableIdAsync(adisyonId);
                if (sourceTableId == null)
                {
                    result = new("Paket siparişlerden ürün aktarılamaz.");
                    return;
                }
                await TableLock.AcquireAsync(_context, sourceTableId.Value, targetTableId);

                var source = await LoadAdisyonAsync(adisyonId);
                var targetTable = await _context.RestaurantTables.FindAsync(targetTableId);
                if (source == null || targetTable == null)
                {
                    result = new("Adisyon ya da hedef masa bulunamadı.");
                    return;
                }
                if (source.TableId != sourceTableId)
                {
                    result = new(MovedMeanwhile);
                    return;
                }

                var reason = TableTransferRules.ItemTransferBlockReason(source, targetTableId, selected, await HasPendingVoidAsync(source.Id));
                if (reason != null)
                {
                    result = new(reason);
                    return;
                }

                var sourceTable = source.Table!;
                sourceName = sourceTable.Name;
                targetName = targetTable.Name;
                var target = await ActiveOnTableAsync(targetTableId);
                var movesEverything = source.Orders.Where(o => !o.IsDraft).SelectMany(o => o.Items)
                    .Where(i => i.Status != OrderStatus.IptalEdildi)
                    .All(i => selected.TryGetValue(i.Id, out var q) && q == i.Quantity);

                // Tüm ürünler boş masaya geçiyorsa bu bir masa taşımasıdır; adisyonun kendisi taşınır.
                if (target == null && movesEverything)
                {
                    await MoveWholeAsync(source, sourceTable, targetTable);
                    result = new(null, source.Id, targetTableId);
                    return;
                }

                if (target == null)
                {
                    target = new Adisyon { TableId = targetTableId, Status = AdisyonStatus.Acik };
                    _context.Adisyonlar.Add(target);
                }

                foreach (var sourceOrder in source.Orders.Where(o => !o.IsDraft).ToList())
                {
                    var moving = sourceOrder.Items.Where(i => selected.ContainsKey(i.Id)).ToList();
                    if (moving.Count == 0)
                    {
                        continue;
                    }

                    var newOrder = new Order
                    {
                        TableId = targetTableId,
                        Adisyon = target,
                        IsDraft = false,
                        Source = sourceOrder.Source,
                        CreatedAt = sourceOrder.CreatedAt,
                        SentAt = sourceOrder.SentAt,
                        Notes = sourceOrder.Notes,
                        CreatedByUserId = sourceOrder.CreatedByUserId,
                        CreatedByUserName = sourceOrder.CreatedByUserName
                    };
                    foreach (var item in moving)
                    {
                        if (selected[item.Id] == item.Quantity)
                        {
                            sourceOrder.Items.Remove(item);
                            newOrder.Items.Add(item);
                        }
                        else
                        {
                            newOrder.Items.Add(TableTransferRules.SplitOff(item, selected[item.Id]));
                        }
                    }

                    OrderStatusHelper.Recompute(newOrder);
                    _context.Orders.Add(newOrder);
                    if (sourceOrder.Items.Count == 0)
                    {
                        _context.Orders.Remove(sourceOrder);
                    }
                    else
                    {
                        OrderStatusHelper.Recompute(sourceOrder);
                    }
                }

                if (targetTable.Status == TableStatus.Bos)
                {
                    targetTable.Status = TableStatus.Dolu;
                }

                var remaining = source.Orders.Where(o => !o.IsDraft && _context.Entry(o).State != EntityState.Deleted)
                    .SelectMany(o => o.Items).Any(i => i.Status != OrderStatus.IptalEdildi);
                MarkChanged(target);
                if (!remaining)
                {
                    CloseAsMerged(source, target.Id);
                    sourceTable.Status = TableStatus.Bos;
                    result = new(null, target.Id, targetTableId);
                }
                else
                {
                    MarkChanged(source);
                    result = new(null, source.Id, sourceTableId);
                }
            });
        }
        catch (DbUpdateConcurrencyException)
        {
            return new(ChangedMeanwhile);
        }

        if (result.Error == null)
        {
            var details = $"{sourceName} → {targetName}: " + string.Join(", ", selected.Select(s => $"#{s.Key} x{s.Value}"));
            await _audit.LogAsync(userId, userName, "Ürün aktarıldı", entityName: "Adisyon", entityId: adisyonId.ToString(), details: details);
            await _realtime.NotifyManyAsync(new[] { "mutfak", "bar", "staff" }, "table-transfer", $"{sourceName} → {targetName}");
        }
        return result;
    }

    private const string ChangedMeanwhile = "Adisyon aynı sırada başka bir işlemle değiştirildi. Sayfayı yenileyip tekrar deneyin.";
    private const string MovedMeanwhile = "Adisyon aynı sırada başka bir masaya taşındı. Sayfayı yenileyip tekrar deneyin.";

    // Kilitlenecek kaynak masa kilitten önce okunur; kilit alındıktan sonra adisyon hâlâ o masadaysa işlem sürer.
    private Task<int?> SourceTableIdAsync(int adisyonId) =>
        _context.Adisyonlar.AsNoTracking().Where(a => a.Id == adisyonId).Select(a => a.TableId).FirstOrDefaultAsync();

    private Task<Adisyon?> LoadAdisyonAsync(int adisyonId) =>
        _context.Adisyonlar.Include(a => a.Table).IncludeTotals().Include(a => a.DiscountRequests)
            .FirstOrDefaultAsync(a => a.Id == adisyonId);

    private Task<Adisyon?> ActiveOnTableAsync(int tableId) =>
        _context.Adisyonlar.IncludeTotals().Include(a => a.DiscountRequests)
            .Where(a => a.TableId == tableId && Adisyon.ActiveStatuses.Contains(a.Status))
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefaultAsync();

    // Adisyon, siparişleri ve masanın gönderilmemiş taslakları birlikte boş masaya geçer.
    private async Task MoveWholeAsync(Adisyon source, RestaurantTable sourceTable, RestaurantTable targetTable)
    {
        source.TableId = targetTable.Id;
        foreach (var order in source.Orders.Concat(await DraftsOnTableAsync(sourceTable.Id)))
        {
            order.TableId = targetTable.Id;
        }
        targetTable.Status = sourceTable.Status;
        sourceTable.Status = TableStatus.Bos;
        MarkChanged(source);
    }

    private Task<List<Order>> DraftsOnTableAsync(int tableId) =>
        _context.Orders.Where(o => o.TableId == tableId && o.IsDraft && o.AdisyonId == null).ToListAsync();

    private Task<bool> HasPendingVoidAsync(int adisyonId) =>
        _context.VoidRequests.AnyAsync(v => v.AdisyonId == adisyonId && v.Status == DiscountRequestStatus.Beklemede);

    private void CloseAsMerged(Adisyon source, int targetId)
    {
        source.Status = AdisyonStatus.Birlestirildi;
        source.MergedIntoAdisyonId = targetId;
        source.ClosedAt = DateTime.Now;
        MarkChanged(source);
    }

    // Durum değişmese de satır güncellenir; RowVersion kontrolü eşzamanlı ikinci işlemi reddeder.
    private void MarkChanged(Adisyon adisyon)
    {
        var entry = _context.Entry(adisyon);
        if (entry.State != EntityState.Added)
        {
            entry.Property(a => a.Status).IsModified = true;
        }
    }
}

public sealed record TransferPanel(Adisyon Adisyon, IReadOnlyList<TransferTarget> Targets, string MoveUrl, string ItemsUrl);
