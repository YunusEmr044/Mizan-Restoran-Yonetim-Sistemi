using Microsoft.EntityFrameworkCore;
using RestoranYonetim.Data;
using RestoranYonetim.Models;
using RestoranYonetim.Security;

namespace RestoranYonetim.Services;

// Stok hareketlerinin merkezi mantığı: tüm giriş/çıkışlar buradan geçer ve InventoryTransaction'a loglanır.
public class InventoryService
{
    private readonly ApplicationDbContext _context;
    private readonly RealtimeNotifier _realtime;

    public InventoryService(ApplicationDbContext context, RealtimeNotifier realtime)
    {
        _context = context;
        _realtime = realtime;
    }

    private async Task<Dictionary<int, decimal>> ComputeUsageAsync(IEnumerable<OrderItem> items)
    {
        var itemList = items.ToList();
        var menuItemIds = itemList.Select(i => i.MenuItemId).Distinct().ToList();
        var recipes = await _context.RecipeItems
            .AsNoTracking()
            .Where(r => menuItemIds.Contains(r.MenuItemId))
            .ToListAsync();

        // Aynı malzeme birden fazla kalemde geçebilir; InventoryItemId bazında tek toplam düşüme indirgenir.
        var usageByInventoryItem = new Dictionary<int, decimal>();
        foreach (var orderItem in itemList)
        {
            foreach (var recipe in recipes.Where(r => r.MenuItemId == orderItem.MenuItemId))
            {
                usageByInventoryItem.TryGetValue(recipe.InventoryItemId, out var existing);
                usageByInventoryItem[recipe.InventoryItemId] = existing + (recipe.Quantity * orderItem.Quantity);
            }
        }

        return usageByInventoryItem;
    }

    private async Task ApplyUsageAsync(Dictionary<int, decimal> usageByInventoryItem, decimal sign, InventoryTransactionType type, Func<decimal, string> note)
    {
        foreach (var (inventoryItemId, totalUsed) in usageByInventoryItem)
        {
            if (totalUsed == 0)
            {
                continue;
            }

            var change = sign * totalUsed;
            var affected = await _context.InventoryItems
                .Where(i => i.Id == inventoryItemId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(i => i.CurrentQuantity, i => i.CurrentQuantity + change));

            if (affected == 0)
            {
                // Malzeme kaydı silinmiş olabilir; sessizce atla, sipariş akışını bozmasın.
                continue;
            }

            _context.InventoryTransactions.Add(new InventoryTransaction
            {
                InventoryItemId = inventoryItemId,
                Type = type,
                QuantityChange = change,
                Note = note(totalUsed)
            });
        }
    }

    public async Task RestoreForItemsAsync(int orderId, IEnumerable<OrderItem> items)
    {
        var usageByInventoryItem = await ComputeUsageAsync(items);
        await ApplyUsageAsync(usageByInventoryItem, +1, InventoryTransactionType.SiparisIptali,
            used => $"Sipariş #{orderId} iptali - stok iadesi ({used:0.###})");
    }

    // ÖNEMLİ: Stok düşümü ExecuteUpdateAsync ile atomik UPDATE olarak yapılır (CurrentQuantity = CurrentQuantity - @miktar).
    // Bellekte oku-çıkar-kaydet yapılırsa eşzamanlı siparişlerde "lost update" oluşur (bir düşüm sessizce kaybolur).
    public async Task DeductForOrderAsync(Order order)
    {
        var usageByInventoryItem = await ComputeUsageAsync(order.Items);

        if (usageByInventoryItem.Count == 0)
        {
            return;
        }

        await ApplyUsageAsync(usageByInventoryItem, -1, InventoryTransactionType.SatisDususu,
            used => $"Sipariş #{order.Id} - satış düşümü ({used:0.###})");

        // ExecuteUpdateAsync change tracker'dan geçmez, bu yüzden güncel miktarlar tekrar okunuyor.
        // Nadiren aynı eşik için birden fazla "kritik stok" bildirimi gidebilir; kabul edilebilir bir ödünleşim.
        var updatedItems = await _context.InventoryItems
            .AsNoTracking()
            .Where(i => usageByInventoryItem.Keys.Contains(i.Id))
            .ToListAsync();

        foreach (var item in updatedItems.Where(i => i.IsLowStock))
        {
            _context.Notifications.Add(new Notification
            {
                Message = $"Kritik stok seviyesi: {item.Name} ({item.CurrentQuantity:0.###} {item.Unit} kaldı)",
                Link = "/Admin/Inventory",
                RequiredPermission = Permissions.Inventory.View
            });
            await _realtime.NotifyAsync("notifications", "low-stock", $"Kritik stok: {item.Name}");
        }
    }

    public void ReceivePurchase(Purchase purchase)
    {
        foreach (var item in purchase.Items)
        {
            if (item.InventoryItem != null)
            {
                item.InventoryItem.CurrentQuantity += item.Quantity;
                item.InventoryItem.UnitCost = item.UnitPrice;

                _context.InventoryTransactions.Add(new InventoryTransaction
                {
                    InventoryItemId = item.InventoryItem.Id,
                    Type = InventoryTransactionType.SatinAlma,
                    QuantityChange = item.Quantity,
                    Note = $"Satın alma - {purchase.Supplier?.Name} ({purchase.InvoiceNumber})"
                });
            }
        }
    }

    public async Task AdjustAsync(int inventoryItemId, decimal newQuantity, string? note)
    {
        var item = await _context.InventoryItems.FindAsync(inventoryItemId);
        if (item == null)
        {
            return;
        }

        var diff = newQuantity - item.CurrentQuantity;
        item.CurrentQuantity = newQuantity;

        _context.InventoryTransactions.Add(new InventoryTransaction
        {
            InventoryItemId = item.Id,
            Type = InventoryTransactionType.ManuelDuzeltme,
            QuantityChange = diff,
            Note = note
        });

        await _context.SaveChangesAsync();
    }
}
