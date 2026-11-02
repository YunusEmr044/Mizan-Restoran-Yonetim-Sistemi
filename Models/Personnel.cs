using System.ComponentModel.DataAnnotations;

namespace RestoranYonetim.Models;

// Haftalık vardiya planındaki tek bir vardiya. Bitiş başlangıçtan küçükse vardiya gece yarısını geçer.
public class StaffShift
{
    public int Id { get; set; }

    [StringLength(450)]
    public string UserId { get; set; } = string.Empty;

    [StringLength(256)]
    public string UserName { get; set; } = string.Empty;

    public DateTime Date { get; set; }
    public TimeSpan Start { get; set; }
    public TimeSpan End { get; set; }

    [StringLength(200)]
    public string? Note { get; set; }
}

// Personelin kendi hesabından girdiği işe başlama/çıkış kaydı.
public class TimeEntry
{
    public int Id { get; set; }

    [StringLength(450)]
    public string UserId { get; set; } = string.Empty;

    [StringLength(256)]
    public string UserName { get; set; } = string.Empty;

    public DateTime ClockIn { get; set; }
    public DateTime? ClockOut { get; set; }

    [StringLength(200)]
    public string? Note { get; set; }
}

public enum LeaveType
{
    [Display(Name = "Yıllık izin")]
    Yillik = 0,
    [Display(Name = "Hastalık")]
    Hastalik = 1,
    [Display(Name = "Ücretsiz izin")]
    Ucretsiz = 2,
    [Display(Name = "Diğer")]
    Diger = 3
}

public enum LeaveStatus
{
    [Display(Name = "Beklemede")]
    Beklemede = 0,
    [Display(Name = "Onaylandı")]
    Onaylandi = 1,
    [Display(Name = "Reddedildi")]
    Reddedildi = 2,
    [Display(Name = "Geri çekildi")]
    GeriCekildi = 3
}

public class LeaveRequest
{
    public int Id { get; set; }

    [StringLength(450)]
    public string UserId { get; set; } = string.Empty;

    [StringLength(256)]
    public string UserName { get; set; } = string.Empty;

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public LeaveType Type { get; set; }

    [StringLength(300)]
    public string? Reason { get; set; }

    public LeaveStatus Status { get; set; } = LeaveStatus.Beklemede;

    [StringLength(256)]
    public string? DecidedByUserName { get; set; }

    public DateTime? DecidedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
