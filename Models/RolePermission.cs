namespace RestoranYonetim.Models;

public class RolePermission
{
    public int Id { get; set; }
    public string RoleName { get; set; } = string.Empty;
    public string Permission { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
}
