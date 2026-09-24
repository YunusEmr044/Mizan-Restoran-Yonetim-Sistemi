namespace RestoranYonetim.Models;

public class FeatureSetting
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Permission { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
}
