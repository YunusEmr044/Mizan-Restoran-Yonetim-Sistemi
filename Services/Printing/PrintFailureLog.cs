using RestoranYonetim.Models;
using RestoranYonetim.Security;

namespace RestoranYonetim.Services.Printing;

public sealed record PrintFailure(Guid Id, MenuDepartment Department, string PrinterName, string Label, DateTime At, string Error)
{
    public string Message => $"Mutfak yazıcısı: {Label} fişi basılamadı ({PrinterName})";

    public static string ScreenPath(MenuDepartment department) => department == MenuDepartment.Bar ? "/Bar" : "/Mutfak";

    public static string ScreenPermission(MenuDepartment department) =>
        department == MenuDepartment.Bar ? Permissions.Orders.Bar : Permissions.Orders.Kitchen;

    public static string RealtimeGroup(MenuDepartment department) => department == MenuDepartment.Bar ? "bar" : "mutfak";
}

// Basılamayan fişlerin bellek içi kaydı (kuyruk yok); mutfak/bar ekranındaki uyarı buradan beslenir.
public class PrintFailureLog
{
    private static readonly TimeSpan Window = TimeSpan.FromHours(2);
    private const int MaxPerDepartment = 20;

    private readonly object _gate = new();
    private readonly List<PrintFailure> _items = new();

    public void Add(PrintFailure failure)
    {
        lock (_gate)
        {
            _items.Add(failure);
            var cutoff = failure.At - Window;
            _items.RemoveAll(f => f.At < cutoff);
        }
    }

    public IReadOnlyList<PrintFailure> Recent(MenuDepartment department, DateTime now)
    {
        lock (_gate)
        {
            return _items.Where(f => f.Department == department && f.At >= now - Window)
                .OrderByDescending(f => f.At).Take(MaxPerDepartment).ToList();
        }
    }
}
