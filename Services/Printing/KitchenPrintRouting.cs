using RestoranYonetim.Models;

namespace RestoranYonetim.Services.Printing;

public static class KitchenPrintRouting
{
    public static IReadOnlyList<KitchenPrinter> Select(IEnumerable<KitchenPrinter> printers, int branchId, MenuDepartment department) =>
        printers.Where(p => p.IsActive && p.BranchId == branchId && p.Department == department).OrderBy(p => p.Id).ToList();
}
