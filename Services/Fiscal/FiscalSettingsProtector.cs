using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;

namespace RestoranYonetim.Services.Fiscal;

// Cihaz ayarları JSON olarak saklanır; gizli alanlar Data Protection ile şifrelenir, panel formuna hiç yazılmaz.
public sealed class FiscalSettingsProtector
{
    private const string SecretPrefix = "enc:";
    private readonly IDataProtector _protector;

    public FiscalSettingsProtector(IDataProtectionProvider provider)
    {
        _protector = provider.CreateProtector("Mizan.FiscalDeviceSettings.v1");
    }

    public (string? Json, string? Error) Protect(IReadOnlyList<FiscalSettingField> fields, IDictionary<string, string?> input, string? existingJson)
    {
        var existing = Raw(existingJson);
        var stored = new Dictionary<string, string>();
        foreach (var field in fields)
        {
            var value = input.TryGetValue(field.Key, out var v) ? v?.Trim() ?? string.Empty : string.Empty;
            if (field.Kind == FiscalFieldKind.Secret)
            {
                if (value.Length > 0)
                {
                    stored[field.Key] = SecretPrefix + _protector.Protect(value);
                }
                else if (existing.TryGetValue(field.Key, out var previous))
                {
                    stored[field.Key] = previous;
                }
            }
            else if (value.Length > 0)
            {
                if (field.Kind == FiscalFieldKind.Number && !decimal.TryParse(value, out _))
                {
                    return (null, $"{field.Label}: sayı girin.");
                }
                if (field.Kind == FiscalFieldKind.Select && field.Options?.Any(o => o.Value == value) == false)
                {
                    return (null, $"{field.Label}: geçersiz seçim.");
                }
                stored[field.Key] = value;
            }

            if (field.Required && !stored.ContainsKey(field.Key))
            {
                return (null, $"{field.Label} zorunlu.");
            }
        }
        return (JsonSerializer.Serialize(stored), null);
    }

    public Dictionary<string, string> Unprotect(string? json) =>
        Raw(json).ToDictionary(p => p.Key, p => p.Value.StartsWith(SecretPrefix, StringComparison.Ordinal) ? _protector.Unprotect(p.Value[SecretPrefix.Length..]) : p.Value);

    // Form için değerler: gizli alanlar her zaman boş döner.
    public Dictionary<string, string> FormValues(IReadOnlyList<FiscalSettingField> fields, string? json)
    {
        var raw = Raw(json);
        return fields.ToDictionary(f => f.Key, f => f.Kind == FiscalFieldKind.Secret ? string.Empty : raw.GetValueOrDefault(f.Key, string.Empty));
    }

    public static bool HasValue(string? json, string key) => Raw(json).ContainsKey(key);

    private static Dictionary<string, string> Raw(string? json) =>
        string.IsNullOrWhiteSpace(json) ? new() : JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
}
