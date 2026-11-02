using System.ComponentModel.DataAnnotations;

namespace RestoranYonetim.Models;

// Ürünün hangi ekrana (mutfak/bar) düşeceğini belirler.
public enum MenuDepartment
{
    [Display(Name = "Mutfak")]
    Mutfak = 0,
    [Display(Name = "Bar")]
    Bar = 1
}
