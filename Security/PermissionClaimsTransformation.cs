using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using RestoranYonetim.Data;

namespace RestoranYonetim.Security;

// Her istekte çerezdeki izin claim'lerini günceller; mantık PermissionResolver'da ortak tutulur ve sonuç PermissionCacheVersion ile anahtarlanarak IMemoryCache'te tutulur.
public sealed class PermissionClaimsTransformation : IClaimsTransformation
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<PermissionClaimsTransformation> _logger;
    private readonly IMemoryCache _cache;
    private readonly PermissionCacheVersion _cacheVersion;

    // Cache stampede'i önlemek için anahtar başına kilit (bkz. SiteContentCache.cs'teki aynı desen).
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

    public PermissionClaimsTransformation(
        ApplicationDbContext context,
        ILogger<PermissionClaimsTransformation> logger,
        IMemoryCache cache,
        PermissionCacheVersion cacheVersion)
    {
        _context = context;
        _logger = logger;
        _cache = cache;
        _cacheVersion = cacheVersion;
    }

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated)
        {
            return principal;
        }

        var roles = identity.FindAll(ClaimTypes.Role)
            .Select(claim => claim.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (roles.Length == 0)
        {
            return principal;
        }

        var cacheKey = $"perm:{_cacheVersion.Current}:{string.Join('|', roles.OrderBy(r => r, StringComparer.OrdinalIgnoreCase))}";
        if (!_cache.TryGetValue(cacheKey, out HashSet<string>? permissions) || permissions == null)
        {
            var gate = _locks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync();
            try
            {
                if (!_cache.TryGetValue(cacheKey, out permissions) || permissions == null)
                {
                    permissions = await PermissionResolver.ResolveAsync(_context, roles, _logger);
                    _cache.Set(cacheKey, permissions, TimeSpan.FromMinutes(5));
                }
            }
            finally
            {
                gate.Release();
            }
        }

        var existingPermissions = identity.FindAll(Permissions.ClaimType)
            .Select(claim => claim.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Artık geçersiz claim'leri de çerezden temizle (sadece eksikleri eklemek yetmez), ki izin değişikliği zaten giriş yapmış kullanıcılara da yansısın.
        foreach (var claim in identity.FindAll(Permissions.ClaimType).ToList())
        {
            if (!permissions.Contains(claim.Value))
            {
                identity.RemoveClaim(claim);
            }
        }

        foreach (var permission in permissions)
        {
            if (!existingPermissions.Contains(permission))
            {
                identity.AddClaim(new Claim(Permissions.ClaimType, permission));
            }
        }

        return principal;
    }
}
