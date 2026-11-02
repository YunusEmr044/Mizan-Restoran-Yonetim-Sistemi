using Microsoft.EntityFrameworkCore;
using RestoranYonetim.Data;
using RestoranYonetim.Models;

namespace RestoranYonetim.Services;

// Hafif sadakat mantığı: telefon numarasına göre müşteri bul/oluştur, harcama/puan biriktirir, seviye otomatik yükselir.
public class CustomerService
{
    private readonly ApplicationDbContext _context;

    private const decimal PointsPerTl = 10m; // her 10 TL harcamaya 1 puan
    public const decimal SilverThreshold = 1000m;
    public const decimal GoldThreshold = 5000m;
    public const decimal VipThreshold = 15000m;

    public static CustomerLevel LevelFor(decimal totalSpent) =>
        totalSpent >= VipThreshold ? CustomerLevel.VIP :
        totalSpent >= GoldThreshold ? CustomerLevel.Gold :
        totalSpent >= SilverThreshold ? CustomerLevel.Silver :
        CustomerLevel.Standart;

    public CustomerService(ApplicationDbContext context)
    {
        _context = context;
    }

    // ÖNEMLİ: Harcama/puan/seviye güncellemesi ExecuteUpdateAsync ile atomik UPDATE olarak yapılır;
    // oku-topla-kaydet şeklinde yapılırsa eşzamanlı ödeme onaylarında bir artış sessizce kaybolabilir (lost update).
    public async Task RegisterSpendAsync(string phone, string? name, decimal amount)
    {
        if (string.IsNullOrWhiteSpace(phone) || amount <= 0)
        {
            return;
        }

        var normalizedPhone = PhoneNormalizer.Normalize(phone);
        if (string.IsNullOrEmpty(normalizedPhone))
        {
            return;
        }

        var customerId = await _context.Customers
            .Where(c => c.Phone == normalizedPhone)
            .Select(c => (int?)c.Id)
            .FirstOrDefaultAsync();

        var trimmedName = name?.Trim() ?? string.Empty;
        var pointsEarned = (int)Math.Floor(amount / PointsPerTl);

        // Yeni müşteri kaydedilmez - çağıran SaveChanges yapmalı.
        if (customerId == null)
        {
            _context.Customers.Add(new Customer
            {
                Phone = normalizedPhone,
                Name = trimmedName == string.Empty ? "İsimsiz Müşteri" : trimmedName,
                TotalSpent = amount,
                Points = pointsEarned,
                Level = LevelFor(amount)
            });
            return;
        }

        await _context.Customers
            .Where(c => c.Id == customerId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(c => c.Name, c => trimmedName == string.Empty ? c.Name : trimmedName)
                .SetProperty(c => c.TotalSpent, c => c.TotalSpent + amount)
                .SetProperty(c => c.Points, c => c.Points + pointsEarned)
                .SetProperty(c => c.Level, c =>
                    (c.TotalSpent + amount) >= VipThreshold ? CustomerLevel.VIP :
                    (c.TotalSpent + amount) >= GoldThreshold ? CustomerLevel.Gold :
                    (c.TotalSpent + amount) >= SilverThreshold ? CustomerLevel.Silver :
                    CustomerLevel.Standart));
    }
}
