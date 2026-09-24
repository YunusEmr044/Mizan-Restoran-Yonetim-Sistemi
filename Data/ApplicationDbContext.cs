using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using RestoranYonetim.Models;

namespace RestoranYonetim.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<Restaurant> Restaurants => Set<Restaurant>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<Area> Areas => Set<Area>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<MenuItem> MenuItems => Set<MenuItem>();
    public DbSet<RestaurantTable> RestaurantTables => Set<RestaurantTable>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<WaiterCall> WaiterCalls => Set<WaiterCall>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<SiteContent> SiteContents => Set<SiteContent>();
    public DbSet<GalleryImage> GalleryImages => Set<GalleryImage>();
    public DbSet<Reservation> Reservations => Set<Reservation>();
    public DbSet<MenuItemOption> MenuItemOptions => Set<MenuItemOption>();
    public DbSet<Adisyon> Adisyonlar => Set<Adisyon>();
    public DbSet<OrderItemOption> OrderItemOptions => Set<OrderItemOption>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<CashShift> CashShifts => Set<CashShift>();
    public DbSet<DiscountRequest> DiscountRequests => Set<DiscountRequest>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<RecipeItem> RecipeItems => Set<RecipeItem>();
    public DbSet<Purchase> Purchases => Set<Purchase>();
    public DbSet<PurchaseItem> PurchaseItems => Set<PurchaseItem>();
    public DbSet<InventoryTransaction> InventoryTransactions => Set<InventoryTransaction>();
    public DbSet<Campaign> Campaigns => Set<Campaign>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Feedback> Feedbacks => Set<Feedback>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<NotificationRead> NotificationReads => Set<NotificationRead>();
    public DbSet<FeatureSetting> FeatureSettings => Set<FeatureSetting>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    public DbSet<HeroSlide> HeroSlides => Set<HeroSlide>();
    public DbSet<HomeStat> HomeStats => Set<HomeStat>();
    public DbSet<HomeFeature> HomeFeatures => Set<HomeFeature>();
    public DbSet<ChefSpecial> ChefSpecials => Set<ChefSpecial>();
    public DbSet<WorkingHoursDay> WorkingHoursDays => Set<WorkingHoursDay>();
    public DbSet<HomeSection> HomeSections => Set<HomeSection>();

    public DbSet<SeoSettings> SeoSettingsRows => Set<SeoSettings>();
    public DbSet<PageSeo> PageSeos => Set<PageSeo>();
    public DbSet<Faq> Faqs => Set<Faq>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<FeatureSetting>()
            .HasIndex(f => f.Key)
            .IsUnique();

        builder.Entity<RolePermission>()
            .HasIndex(p => new { p.RoleName, p.Permission })
            .IsUnique();

        builder.Entity<RestaurantTable>()
            .HasIndex(t => t.QrToken)
            .IsUnique();

        builder.Entity<Branch>()
            .HasOne(b => b.Restaurant)
            .WithMany(r => r.Branches)
            .HasForeignKey(b => b.RestaurantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Area>()
            .HasOne(a => a.Branch)
            .WithMany(b => b.Areas)
            .HasForeignKey(a => a.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<RestaurantTable>()
            .HasOne(t => t.Area)
            .WithMany(a => a.Tables)
            .HasForeignKey(t => t.AreaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<MenuItem>()
            .HasOne(m => m.Category)
            .WithMany(c => c.MenuItems)
            .HasForeignKey(m => m.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Order>()
            .HasOne(o => o.Table)
            .WithMany(t => t.Orders)
            .HasForeignKey(o => o.TableId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<OrderItem>()
            .HasOne(oi => oi.Order)
            .WithMany(o => o.Items)
            .HasForeignKey(oi => oi.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<OrderItem>()
            .HasOne(oi => oi.MenuItem)
            .WithMany()
            .HasForeignKey(oi => oi.MenuItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<WaiterCall>()
            .HasOne(w => w.Table)
            .WithMany()
            .HasForeignKey(w => w.TableId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<MenuItemOption>()
            .HasOne(o => o.MenuItem)
            .WithMany()
            .HasForeignKey(o => o.MenuItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Adisyon>()
            .HasOne(a => a.Table)
            .WithMany()
            .HasForeignKey(a => a.TableId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Order>()
            .HasOne(o => o.Adisyon)
            .WithMany(a => a.Orders)
            .HasForeignKey(o => o.AdisyonId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<OrderItemOption>()
            .HasOne(oio => oio.OrderItem)
            .WithMany(oi => oi.Options)
            .HasForeignKey(oio => oio.OrderItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Payment>()
            .HasOne(p => p.Adisyon)
            .WithMany(a => a.Payments)
            .HasForeignKey(p => p.AdisyonId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Payment>()
            .HasOne(p => p.CashShift)
            .WithMany(s => s.Payments)
            .HasForeignKey(p => p.CashShiftId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<DiscountRequest>()
            .HasOne(d => d.Adisyon)
            .WithMany(a => a.DiscountRequests)
            .HasForeignKey(d => d.AdisyonId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<InventoryItem>()
            .HasOne(i => i.Supplier)
            .WithMany(s => s.InventoryItems)
            .HasForeignKey(i => i.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<RecipeItem>()
            .HasOne(r => r.MenuItem)
            .WithMany(m => m.RecipeItems)
            .HasForeignKey(r => r.MenuItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<RecipeItem>()
            .HasOne(r => r.InventoryItem)
            .WithMany()
            .HasForeignKey(r => r.InventoryItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Purchase>()
            .HasOne(p => p.Supplier)
            .WithMany(s => s.Purchases)
            .HasForeignKey(p => p.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<PurchaseItem>()
            .HasOne(pi => pi.Purchase)
            .WithMany(p => p.Items)
            .HasForeignKey(pi => pi.PurchaseId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<PurchaseItem>()
            .HasOne(pi => pi.InventoryItem)
            .WithMany()
            .HasForeignKey(pi => pi.InventoryItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<InventoryTransaction>()
            .HasOne(t => t.InventoryItem)
            .WithMany()
            .HasForeignKey(t => t.InventoryItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Feedback>()
            .HasOne(f => f.Table)
            .WithMany()
            .HasForeignKey(f => f.TableId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Order>()
            .HasIndex(o => new { o.IsDraft, o.Status });

        builder.Entity<Order>()
            .HasIndex(o => o.SentAt);

        builder.Entity<Adisyon>()
            .HasIndex(a => new { a.Status, a.ClosedAt });

        builder.Entity<Adisyon>()
            .HasIndex(a => new { a.TableId, a.Status });

        builder.Entity<WaiterCall>()
            .HasIndex(w => w.Resolved);

        builder.Entity<Payment>()
            .HasIndex(p => p.CreatedAt);

        builder.Entity<Reservation>()
            .HasIndex(r => r.Date);

        builder.Entity<DiscountRequest>()
            .HasIndex(d => d.Status);

        // ÖNEMLİ: Filtrelenmiş unique indeks - "aynı anda en fazla bir açık vardiya olabilir" kuralını DB seviyesinde garanti eder (uygulama tarafı tek başına race condition'a açıktı).
        builder.Entity<CashShift>()
            .HasIndex(s => s.ClosedAt)
            .IsUnique()
            .HasFilter("[ClosedAt] IS NULL");

        builder.Entity<Customer>()
            .HasIndex(c => c.Phone);

        builder.Entity<Notification>()
            .HasIndex(n => n.CreatedAt);

        // ÖNEMLİ: (NotificationId, UserId) en fazla bir kez var olabilir; çift "okundu" denemesi DbUpdateException fırlatır, controller bunu no-op olarak yutar.
        builder.Entity<NotificationRead>()
            .HasIndex(r => new { r.UserId, r.NotificationId })
            .IsUnique();

        builder.Entity<NotificationRead>()
            .HasOne(r => r.Notification)
            .WithMany()
            .HasForeignKey(r => r.NotificationId)
            .OnDelete(DeleteBehavior.Cascade);

        // ChefSpecial -> MenuItem: Restrict - ürün "şefin önerisi" olarak referans alınıyorsa admin onu silemez.
        builder.Entity<ChefSpecial>()
            .HasOne(c => c.MenuItem)
            .WithMany()
            .HasForeignKey(c => c.MenuItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<HomeSection>()
            .HasIndex(s => s.Key)
            .IsUnique();

        builder.Entity<WorkingHoursDay>()
            .HasIndex(d => d.DayOfWeek)
            .IsUnique();

        builder.Entity<HeroSlide>()
            .HasIndex(h => new { h.IsActive, h.DisplayOrder });

        builder.Entity<HomeStat>()
            .HasIndex(s => new { s.IsActive, s.DisplayOrder });

        builder.Entity<HomeFeature>()
            .HasIndex(f => new { f.IsActive, f.DisplayOrder });

        builder.Entity<ChefSpecial>()
            .HasIndex(c => new { c.IsActive, c.StartDate, c.EndDate });

        builder.Entity<MenuItem>()
            .HasIndex(m => new { m.IsFeaturedHome, m.FeaturedOrder });

        builder.Entity<GalleryImage>()
            .HasIndex(g => new { g.ShowOnHomepage, g.DisplayOrder });

        builder.Entity<PageSeo>()
            .HasIndex(p => p.PageKey)
            .IsUnique();

        builder.Entity<Faq>()
            .HasIndex(f => new { f.IsActive, f.DisplayOrder });
    }
}
