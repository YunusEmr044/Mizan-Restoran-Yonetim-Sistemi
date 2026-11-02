using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RestoranYonetim.Data;
using RestoranYonetim.Models;
using RestoranYonetim.Security;
using RestoranYonetim.Services;
using RestoranYonetim.Services.Fiscal;

namespace RestoranYonetim.Areas.Admin.Controllers;

public sealed record FiscalDeviceForm(int Id, int BranchId, string Name, string AdapterKey, bool IsActive, IFiscalAdapter Adapter,
    IReadOnlyDictionary<string, string> Values, IReadOnlySet<string> SecretsSet, IReadOnlyList<Branch> Branches, IReadOnlyList<IFiscalAdapter> Adapters);

public sealed record FiscalDevicesView(IReadOnlyList<FiscalDevice> Devices, IReadOnlyList<FiscalReceipt> Recent, bool ModuleEnabled, FiscalAdapterRegistry Registry);

// Şube başına yazar kasa (ÖKC) cihazı: bağdaştırıcı seçimi, ayarlar (gizli alanlar şifreli), bağlantı testi ve son mali fişler.
[Area("Admin")]
[Authorize(Policy = Permissions.Fiscal.Manage)]
public class FiscalDevicesController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly FiscalAdapterRegistry _registry;
    private readonly FiscalSettingsProtector _settings;
    private readonly FiscalService _fiscal;
    private readonly AuditService _audit;

    public FiscalDevicesController(ApplicationDbContext context, FiscalAdapterRegistry registry, FiscalSettingsProtector settings, FiscalService fiscal, AuditService audit)
    {
        _context = context;
        _registry = registry;
        _settings = settings;
        _fiscal = fiscal;
        _audit = audit;
    }

    public async Task<IActionResult> Index()
    {
        var devices = await _context.FiscalDevices.AsNoTracking().Include(d => d.Branch).OrderBy(d => d.Branch!.Name).ToListAsync();
        var recent = await _context.FiscalReceipts.AsNoTracking().Include(r => r.Adisyon).ThenInclude(a => a!.Table)
            .OrderByDescending(r => r.CreatedAt).Take(50).ToListAsync();
        return View(new FiscalDevicesView(devices, recent, await _fiscal.IsEnabledAsync(), _registry));
    }

    public async Task<IActionResult> Edit(int? id, string? adapter, string? name, int? branchId, bool? isActive)
    {
        var device = id == null ? new FiscalDevice { AdapterKey = SimulatorFiscalAdapter.AdapterKey } : await _context.FiscalDevices.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id);
        if (device == null)
        {
            return NotFound();
        }
        // Bağdaştırıcı değiştirilince form o bağdaştırıcının alanlarıyla yeniden çizilir; ad, şube ve aktiflik korunur, ayar alanları yeni bağdaştırıcıya göre boş gelir.
        device.Name = name ?? device.Name;
        device.BranchId = branchId ?? device.BranchId;
        device.IsActive = isActive ?? device.IsActive;
        var key = _registry.Get(adapter) != null ? adapter! : device.AdapterKey;
        return View(await FormAsync(device, key, key == device.AdapterKey ? null : new Dictionary<string, string>()));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(int id, int branchId, string? name, string adapterKey, bool isActive, [FromForm] Dictionary<string, string?>? settings)
    {
        var adapter = _registry.Get(adapterKey);
        if (adapter == null)
        {
            return BadRequest();
        }
        var device = id == 0 ? new FiscalDevice() : await _context.FiscalDevices.FirstOrDefaultAsync(d => d.Id == id);
        if (device == null)
        {
            return NotFound();
        }

        var errors = new List<string>();
        if (!await _context.Branches.AnyAsync(b => b.Id == branchId))
        {
            errors.Add("Şube seçin.");
        }
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > FiscalDevice.NameMax)
        {
            errors.Add($"Ad zorunlu ve en fazla {FiscalDevice.NameMax} karakter olabilir.");
        }
        if (isActive && await _context.FiscalDevices.AnyAsync(d => d.BranchId == branchId && d.IsActive && d.Id != id))
        {
            errors.Add("Bu şubede başka bir aktif yazar kasa var; önce onu pasife alın.");
        }
        var (json, settingsError) = _settings.Protect(adapter.SettingFields, settings ?? new(), device.AdapterKey == adapterKey ? device.SettingsJson : null);
        if (settingsError != null)
        {
            errors.Add(settingsError);
        }
        if (errors.Count > 0)
        {
            errors.ForEach(e => ModelState.AddModelError(string.Empty, e));
            var draft = new FiscalDevice { Id = id, BranchId = branchId, Name = name?.Trim() ?? string.Empty, AdapterKey = adapterKey, IsActive = isActive, SettingsJson = device.SettingsJson };
            return View("Edit", await FormAsync(draft, adapterKey, settings?.ToDictionary(p => p.Key, p => p.Value ?? string.Empty)));
        }

        device.BranchId = branchId;
        device.Name = name!.Trim();
        device.AdapterKey = adapterKey;
        device.IsActive = isActive;
        device.SettingsJson = json!;
        device.UpdatedAt = DateTime.Now;
        if (id == 0)
        {
            _context.FiscalDevices.Add(device);
        }
        await _context.SaveChangesAsync();
        await _audit.LogAsync(User.FindFirstValue(ClaimTypes.NameIdentifier), User.Identity?.Name, id == 0 ? "Yazar kasa eklendi" : "Yazar kasa güncellendi",
            entityName: "FiscalDevice", entityId: device.Id.ToString(), details: $"{device.Name} · {adapter.DisplayName} · {(device.IsActive ? "aktif" : "pasif")}");
        TempData["Info"] = $"{device.Name} kaydedildi.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Test(int id)
    {
        var device = await _context.FiscalDevices.AsNoTracking().FirstOrDefaultAsync(d => d.Id == id);
        if (device == null || _registry.Get(device.AdapterKey) is not { } adapter)
        {
            return NotFound();
        }
        FiscalResult result;
        using (var timeout = new CancellationTokenSource(FiscalService.ReceiptTimeout))
        {
            try
            {
                result = await adapter.TestAsync(_fiscal.Context(device), timeout.Token);
            }
            catch (Exception ex)
            {
                result = new FiscalResult(FiscalStatus.Hata, Message: ex is OperationCanceledException ? "Cihaz zamanında yanıt vermedi." : "Cihaza bağlanılamadı.");
            }
        }
        TempData[result.Status == FiscalStatus.Basarili ? "Info" : "Error"] = $"{device.Name}: {result.Message ?? result.Status.ToString()}";
        return RedirectToAction(nameof(Index));
    }

    private async Task<FiscalDeviceForm> FormAsync(FiscalDevice device, string adapterKey, IReadOnlyDictionary<string, string>? posted)
    {
        var adapter = _registry.Get(adapterKey)!;
        var sameAdapter = adapterKey == device.AdapterKey;
        var stored = sameAdapter ? _settings.FormValues(adapter.SettingFields, device.SettingsJson) : new Dictionary<string, string>();
        var values = adapter.SettingFields.ToDictionary(f => f.Key,
            f => f.Kind == FiscalFieldKind.Secret ? string.Empty : posted?.GetValueOrDefault(f.Key) ?? stored.GetValueOrDefault(f.Key, string.Empty));
        var secretsSet = adapter.SettingFields.Where(f => f.Kind == FiscalFieldKind.Secret && sameAdapter && FiscalSettingsProtector.HasValue(device.SettingsJson, f.Key))
            .Select(f => f.Key).ToHashSet();
        var branches = await _context.Branches.AsNoTracking().OrderBy(b => b.Name).ToListAsync();
        return new FiscalDeviceForm(device.Id, device.BranchId, device.Name, adapterKey, device.IsActive, adapter, values, secretsSet, branches, _registry.All);
    }
}
