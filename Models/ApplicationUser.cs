using Microsoft.AspNetCore.Identity;

namespace RestoranYonetim.Models;

public class ApplicationUser : IdentityUser
{
    public string? FullName { get; set; }
}
