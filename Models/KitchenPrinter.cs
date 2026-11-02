using System.ComponentModel.DataAnnotations;

namespace RestoranYonetim.Models;

// Şube ve bölüm (mutfak/bar) yazıcısı; doğrulama kuralları KitchenPrinterValidation'dadır.
public class KitchenPrinter
{
    public const int NameMax = 100;
    public const int HostMax = 100;
    public const int DefaultPort = 9100;

    public int Id { get; set; }

    [StringLength(NameMax)]
    [Display(Name = "Ad")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Şube")]
    public int BranchId { get; set; }
    public Branch? Branch { get; set; }

    [Display(Name = "Bölüm")]
    public MenuDepartment Department { get; set; }

    [Display(Name = "Bağlantı")]
    public PrinterConnection Connection { get; set; }

    [StringLength(HostMax)]
    [Display(Name = "IP adresi")]
    public string? Host { get; set; }

    [Display(Name = "Port")]
    public int Port { get; set; } = DefaultPort;

    [Display(Name = "Kâğıt genişliği (mm)")]
    public int PaperWidthMm { get; set; } = 80;

    [Display(Name = "Aktif")]
    public bool IsActive { get; set; } = true;
}
