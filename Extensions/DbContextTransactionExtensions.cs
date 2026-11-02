using Microsoft.EntityFrameworkCore;

namespace RestoranYonetim.Extensions;

public static class DbContextTransactionExtensions
{
    // `work` içinde SaveChangesAsync çağrılmamalı - kaydı bu yardımcı yapar.
    public static async Task ExecuteInTransactionAsync(this DbContext context, Func<Task> work)
    {
        var strategy = context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            var addedBefore = context.ChangeTracker.Entries().Where(e => e.State == EntityState.Added).Select(e => e.Entity).ToHashSet();
            try
            {
                await using var transaction = await context.Database.BeginTransactionAsync();
                await work();
                await context.SaveChangesAsync(acceptAllChangesOnSuccess: false);
                await transaction.CommitAsync();
            }
            catch
            {
                foreach (var entry in context.ChangeTracker.Entries().Where(e => e.State == EntityState.Added && !addedBefore.Contains(e.Entity)).ToList())
                {
                    entry.State = EntityState.Detached;
                }
                throw;
            }
        });
        context.ChangeTracker.AcceptAllChanges();
    }
}
