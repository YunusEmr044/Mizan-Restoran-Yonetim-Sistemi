using System.ComponentModel.DataAnnotations;

namespace RestoranYonetim.Models;

// Şubenin yazar kasa (ÖKC) cihazı; bağdaştırıcı ve ayarları panelden seçilir. Gizli ayarlar şifreli saklanır.
public class FiscalDevice
{
    public const int NameMax = 100;

    public int Id { get; set; }

    [Display(Name = "Şube")]
    public int BranchId { get; set; }
    public Branch? Branch { get; set; }

    [StringLength(50)]
    [Display(Name = "Bağdaştırıcı")]
    public string AdapterKey { get; set; } = string.Empty;

    [StringLength(NameMax)]
    [Display(Name = "Ad")]
    public string Name { get; set; } = string.Empty;

    public string SettingsJson { get; set; } = "{}";

    [Display(Name = "Aktif")]
    public bool IsActive { get; set; } = true;

    public DateTime UpdatedAt { get; set; } = DateTime.Now;
}

public enum FiscalReceiptStatus
{
    [Display(Name = "Bekliyor")]
    Bekliyor = 0,
    [Display(Name = "Kesildi")]
    Kesildi = 1,
    [Display(Name = "Hata")]
    Hata = 2,
    [Display(Name = "Belirsiz")]
    Belirsiz = 3,
    [Display(Name = "Elle girilecek")]
    ElleGirilecek = 4,
    [Display(Name = "İptal")]
    Iptal = 5,
    [Display(Name = "Fişsiz kapatıldı")]
    FissizKapatildi = 6
}

// Adisyon başına tek mali fiş kaydı (AdisyonId benzersiz); çift fiş veritabanı seviyesinde engellenir.
public class FiscalReceipt
{
    public int Id { get; set; }

    public int AdisyonId { get; set; }
    public Adisyon? Adisyon { get; set; }

    public int? FiscalDeviceId { get; set; }
    public FiscalDevice? FiscalDevice { get; set; }

    public FiscalReceiptStatus Status { get; set; } = FiscalReceiptStatus.Bekliyor;

    [StringLength(50)]
    public string? ReceiptNo { get; set; }

    [StringLength(20)]
    public string? ZNo { get; set; }

    public decimal Total { get; set; }

    public int Attempts { get; set; }

    [StringLength(500)]
    public string? LastError { get; set; }

    public bool IsSimulated { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime? PrintedAt { get; set; }

    [StringLength(256)]
    public string? PrintedByUserName { get; set; }
}
