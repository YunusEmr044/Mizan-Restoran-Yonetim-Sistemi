using Microsoft.AspNetCore.Mvc;
using RestoranYonetim.Data;
using RestoranYonetim.Security;
using RestoranYonetim.Services;

namespace RestoranYonetim.ViewComponents;

public record AdminNavViewModel(IReadOnlyList<NavGroup> Groups, NavGroup? ActiveGroup, NavItem? ActiveItem, bool CanPreview, string? PreviewRole);

public class AdminNavViewComponent : ViewComponent
{
    private readonly ApplicationDbContext _context;

    public AdminNavViewComponent(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var active = AdminNavigation.Find(RouteData.Values);
        var user = HttpContext.User;
        var preview = await NavPreview.PreviewPrincipalAsync(HttpContext, _context);
        return View(new AdminNavViewModel(AdminNavigation.For(preview ?? user), active?.Group, active?.Item,
            user.IsInRole(RolePermissions.SuperAdmin), NavPreview.RoleFor(HttpContext)));
    }
}
