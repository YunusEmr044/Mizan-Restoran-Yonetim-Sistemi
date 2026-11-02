using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using RestoranYonetim.Data;
using RestoranYonetim.Models;
using RestoranYonetim.Security;
using RestoranYonetim.Services;

namespace RestoranYonetim.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Policy = Permissions.Products.Manage)]
public class MenuItemsController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ImageStorageService _imageStorage;
    private readonly AuditService _audit;
    private readonly IMemoryCache _cache;

    public MenuItemsController(ApplicationDbContext context, AuditService audit, IMemoryCache cache, ImageStorageService imageStorage)
    {
        _context = context;
        _imageStorage = imageStorage;
        _audit = audit;
        _cache = cache;
    }

    private Task<string> SaveImageAsync(IFormFile file, string? nameHint) =>
        _imageStorage.SaveAsync(file, "menu", nameHint, "urun");

    // Görsel değişince/silinince eski dosya da diskten silinir (orphan cleanup); try/catch dosya kilitliyse 500'e çevirmesin diye.
    public async Task<IActionResult> Index()
    {
        var items = await _context.MenuItems
            .AsNoTracking()
            .Include(m => m.Category)
            .Include(m => m.RecipeItems).ThenInclude(r => r.InventoryItem)
            .OrderBy(m => m.Category!.DisplayOrder)
            .ThenBy(m => m.Name)
            .ToListAsync();
        return View(items);
    }

    private async Task PopulateCategoriesAsync(int? selectedId = null)
    {
        var categories = await _context.Categories.AsNoTracking().OrderBy(c => c.DisplayOrder).ToListAsync();
        ViewBag.Categories = new SelectList(categories, "Id", "Name", selectedId);
    }

    public async Task<IActionResult> Create()
    {
        await PopulateCategoriesAsync();
        return View(new MenuItem());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(MenuItem menuItem, IFormFile? imageFile)
    {
        if (!ModelState.IsValid)
        {
            await PopulateCategoriesAsync(menuItem.CategoryId);
            return View(menuItem);
        }

        if (imageFile is { Length: > 0 })
        {
            var uploadError = await ImageUploadValidator.ValidateAsync(imageFile);
            if (uploadError != null)
            {
                ModelState.AddModelError(string.Empty, uploadError);
                await PopulateCategoriesAsync(menuItem.CategoryId);
                return View(menuItem);
            }
            menuItem.ImageUrl = await SaveImageAsync(imageFile, menuItem.Name);
        }

        _context.MenuItems.Add(menuItem);
        await _context.SaveChangesAsync();
        MenuCache.Invalidate(_cache);
        HomePreviewCache.InvalidateFeaturedMenu(_cache);
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var item = await _context.MenuItems.FindAsync(id);
        if (item == null)
        {
            return NotFound();
        }
        await PopulateCategoriesAsync(item.CategoryId);
        return View(item);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, MenuItem menuItem, IFormFile? imageFile, bool removeImage = false)
    {
        if (id != menuItem.Id)
        {
            return NotFound();
        }
        if (!ModelState.IsValid)
        {
            await PopulateCategoriesAsync(menuItem.CategoryId);
            return View(menuItem);
        }

        // ÖNEMLİ: Edit view'inde ImageUrl için <input> yoktu - yeni görsel yüklenmeyen her düzenlemede model binding ImageUrl=null gönderip mevcut görseli sessizce siliyordu. Mevcut kayıt çekilip ImageUrl DB'deki değerle başlatılıyor, aşağıdaki koşullarda bilinçli değiştiriliyor.
        var existing = await _context.MenuItems.AsNoTracking().FirstOrDefaultAsync(m => m.Id == id);
        if (existing == null)
        {
            return NotFound();
        }
        menuItem.ImageUrl = existing.ImageUrl;

        if (imageFile is { Length: > 0 })
        {
            var uploadError = await ImageUploadValidator.ValidateAsync(imageFile);
            if (uploadError != null)
            {
                ModelState.AddModelError(string.Empty, uploadError);
                await PopulateCategoriesAsync(menuItem.CategoryId);
                return View(menuItem);
            }
            menuItem.ImageUrl = await SaveImageAsync(imageFile, menuItem.Name);
        }
        else if (removeImage)
        {
            menuItem.ImageUrl = null;
        }

        _context.Update(menuItem);
        await _context.SaveChangesAsync();
        MenuCache.Invalidate(_cache);
        HomePreviewCache.InvalidateFeaturedMenu(_cache);

        if (existing.ImageUrl != menuItem.ImageUrl)
        {
            _imageStorage.Delete(existing.ImageUrl);
        }

        // Sadece fiyat gerçekten değiştiğinde loglanır (gelir/faturalama etkisi olduğu için).
        if (existing.Price != menuItem.Price)
        {
            await _audit.LogAsync(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                User.Identity?.Name,
                "Ürün fiyatı değiştirildi",
                entityName: "MenuItem",
                entityId: menuItem.Id.ToString(),
                details: $"{menuItem.Name}: {existing.Price:0.00} → {menuItem.Price:0.00}");
        }

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Delete(int id)
    {
        var item = await _context.MenuItems.AsNoTracking().Include(m => m.Category).FirstOrDefaultAsync(m => m.Id == id);
        if (item == null)
        {
            return NotFound();
        }
        return View(item);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var item = await _context.MenuItems.FindAsync(id);
        if (item != null)
        {
            var itemName = item.Name;
            var imageUrl = item.ImageUrl;
            _context.MenuItems.Remove(item);
            await _context.SaveChangesAsync();
            MenuCache.Invalidate(_cache);
            HomePreviewCache.InvalidateFeaturedMenu(_cache);
            _imageStorage.Delete(imageUrl);

            await _audit.LogAsync(
                User.FindFirstValue(ClaimTypes.NameIdentifier),
                User.Identity?.Name,
                "Ürün silindi",
                entityName: "MenuItem",
                entityId: id.ToString(),
                details: itemName);
        }
        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Options(int id)
    {
        var menuItem = await _context.MenuItems.FindAsync(id);
        if (menuItem == null)
        {
            return NotFound();
        }

        ViewBag.MenuItem = menuItem;
        var options = await _context.MenuItemOptions
            .AsNoTracking()
            .Where(o => o.MenuItemId == id)
            .OrderBy(o => o.Name)
            .ToListAsync();
        return View(options);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddOption(int menuItemId, string name, decimal extraPrice)
    {
        var menuItem = await _context.MenuItems.FindAsync(menuItemId);
        if (menuItem == null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            TempData["Error"] = "Seçenek adı girin.";
        }
        else
        {
            _context.MenuItemOptions.Add(new MenuItemOption
            {
                MenuItemId = menuItemId,
                Name = name.Trim(),
                ExtraPrice = extraPrice
            });
            await _context.SaveChangesAsync();
            MenuCache.Invalidate(_cache);
        }

        return RedirectToAction(nameof(Options), new { id = menuItemId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteOption(int id, int menuItemId)
    {
        var option = await _context.MenuItemOptions.FindAsync(id);
        if (option != null)
        {
            _context.MenuItemOptions.Remove(option);
            await _context.SaveChangesAsync();
            MenuCache.Invalidate(_cache);
        }
        return RedirectToAction(nameof(Options), new { id = menuItemId });
    }

    // Reçete: ürünün hangi malzemelerden ne kadar kullandığı - tahmini maliyet/kârlılık buradan hesaplanır.
    [Authorize(Policy = Permissions.Inventory.Manage)]
    public async Task<IActionResult> Recipe(int id)
    {
        var menuItem = await _context.MenuItems.FindAsync(id);
        if (menuItem == null)
        {
            return NotFound();
        }

        ViewBag.MenuItem = menuItem;
        ViewBag.InventoryItems = new SelectList(
            await _context.InventoryItems.AsNoTracking().OrderBy(i => i.Name).ToListAsync(), "Id", "Name");

        var items = await _context.RecipeItems
            .AsNoTracking()
            .Include(r => r.InventoryItem)
            .Where(r => r.MenuItemId == id)
            .OrderBy(r => r.InventoryItem!.Name)
            .ToListAsync();

        var cost = items.Sum(r => r.Quantity * (r.InventoryItem?.UnitCost ?? 0));
        ViewBag.EstimatedCost = cost;
        ViewBag.EstimatedProfit = menuItem.Price - cost;

        return View(items);
    }

    [HttpPost]
    [Authorize(Policy = Permissions.Inventory.Manage)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddRecipeItem(int menuItemId, int inventoryItemId, decimal quantity)
    {
        var menuItem = await _context.MenuItems.FindAsync(menuItemId);
        var inventoryItem = await _context.InventoryItems.FindAsync(inventoryItemId);
        if (menuItem == null || inventoryItem == null)
        {
            return NotFound();
        }

        if (quantity <= 0)
        {
            TempData["Error"] = "Miktar sıfırdan büyük olmalı.";
        }
        else
        {
            _context.RecipeItems.Add(new RecipeItem
            {
                MenuItemId = menuItemId,
                InventoryItemId = inventoryItemId,
                Quantity = quantity
            });
            await _context.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Recipe), new { id = menuItemId });
    }

    [HttpPost]
    [Authorize(Policy = Permissions.Inventory.Manage)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteRecipeItem(int id, int menuItemId)
    {
        var recipeItem = await _context.RecipeItems.FindAsync(id);
        if (recipeItem != null)
        {
            _context.RecipeItems.Remove(recipeItem);
            await _context.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Recipe), new { id = menuItemId });
    }

    public const int MaxImportBytes = 1024 * 1024;

    public IActionResult Import() => View(Array.Empty<MenuCsvError>());

    public IActionResult ImportTemplate() =>
        File(System.Text.Encoding.UTF8.GetPreamble().Concat(System.Text.Encoding.UTF8.GetBytes(MenuCsv.Template)).ToArray(), "text/csv", "menu-sablonu.csv");

    // Hatalı satır varsa hiçbir şey kaydedilmez. Aynı kategoride aynı adlı ürün güncellenir, yoksa eklenir; eksik kategoriler oluşturulur.
    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(MaxImportBytes + 64 * 1024)]
    public async Task<IActionResult> Import(IFormFile? file)
    {
        if (file == null || file.Length == 0 || file.Length > MaxImportBytes)
        {
            return View(new[] { new MenuCsvError(0, "En fazla 1 MB'lık bir CSV dosyası seçin.") });
        }
        byte[] bytes;
        using (var stream = new MemoryStream())
        {
            await file.CopyToAsync(stream);
            bytes = stream.ToArray();
        }
        string text;
        try
        {
            text = new System.Text.UTF8Encoding(false, true).GetString(bytes);
        }
        catch (System.Text.DecoderFallbackException)
        {
            // Türkçe Excel "CSV" kaydını Windows-1254 ile yapar.
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            text = System.Text.Encoding.GetEncoding(1254).GetString(bytes);
        }

        var parsed = MenuCsv.Parse(text);
        if (parsed.Errors.Count > 0)
        {
            return View(parsed.Errors);
        }

        var comparer = StringComparer.Create(new System.Globalization.CultureInfo("tr-TR"), true);
        var categories = await _context.Categories.ToListAsync();
        var items = await _context.MenuItems.ToListAsync();
        var nextOrder = categories.Select(c => c.DisplayOrder).DefaultIfEmpty(0).Max();
        int created = 0, updated = 0, newCategories = 0;
        foreach (var row in parsed.Rows)
        {
            var category = categories.FirstOrDefault(c => comparer.Equals(c.Name, row.Category));
            if (category == null)
            {
                category = new Category { Name = row.Category, DisplayOrder = ++nextOrder };
                categories.Add(category);
                _context.Categories.Add(category);
                newCategories++;
            }
            var item = items.FirstOrDefault(i => (i.Category == category || (category.Id != 0 && i.CategoryId == category.Id)) && comparer.Equals(i.Name, row.Name));
            if (item == null)
            {
                item = new MenuItem { Category = category, Name = row.Name };
                items.Add(item);
                _context.MenuItems.Add(item);
                created++;
            }
            else
            {
                updated++;
            }
            item.Price = row.Price;
            item.Description = row.Description;
            item.Department = row.Department;
            item.VatRate = row.VatRate;
        }
        await _context.SaveChangesAsync();
        MenuCache.Invalidate(_cache);
        HomePreviewCache.InvalidateFeaturedMenu(_cache);
        await _audit.LogAsync(User.FindFirstValue(ClaimTypes.NameIdentifier), User.Identity?.Name, "Menü dosyadan aktarıldı",
            entityName: "MenuItem", details: $"{file.FileName}: {created} yeni, {updated} güncellendi, {newCategories} yeni kategori");
        TempData["Info"] = $"Menü aktarıldı: {created} yeni ürün, {updated} güncellenen ürün, {newCategories} yeni kategori.";
        return RedirectToAction(nameof(Index));
    }
}
