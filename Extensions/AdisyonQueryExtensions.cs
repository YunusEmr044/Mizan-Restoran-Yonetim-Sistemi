using Microsoft.EntityFrameworkCore;
using RestoranYonetim.Models;

namespace RestoranYonetim.Extensions;

public static class AdisyonQueryExtensions
{
    // Tutar (GrandTotal/RemainingTotal) hesaplanacak her adisyon sorgusu bunu kullanmalı.
    public static IQueryable<Adisyon> IncludeTotals(this IQueryable<Adisyon> query) =>
        query
            .Include(a => a.Orders).ThenInclude(o => o.Items).ThenInclude(i => i.Options)
            .Include(a => a.Payments);
}
