using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RestoranYonetim.Data;
using RestoranYonetim.Models;

namespace RestoranYonetim.Security;

// PermissionResolver DB okunamasa bile hata fırlatmaz (fail-closed döner); böylece şema senkron değilken giriş çökmez.
public class ApplicationUserClaimsPrincipalFactory : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>
{
    public ApplicationUserClaimsPrincipalFactory(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        IOptions<IdentityOptions> optionsAccessor,
        ApplicationDbContext context,
        ILogger<ApplicationUserClaimsPrincipalFactory> logger)
        : base(userManager, roleManager, optionsAccessor)
    {
        _context = context;
        _logger = logger;
    }

    private readonly ApplicationDbContext _context;
    private readonly ILogger<ApplicationUserClaimsPrincipalFactory> _logger;

    public override async Task<ClaimsPrincipal> CreateAsync(ApplicationUser user)
    {
        var principal = await base.CreateAsync(user);
        var identity = (ClaimsIdentity)principal.Identity!;

        var roles = await UserManager.GetRolesAsync(user);
        var permissions = await PermissionResolver.ResolveAsync(_context, roles, _logger);

        foreach (var permission in permissions)
        {
            identity.AddClaim(new Claim(Permissions.ClaimType, permission));
        }

        return principal;
    }
}
