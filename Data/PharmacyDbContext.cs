using Microsoft.EntityFrameworkCore;
using PharmacySystem.Models.Entities;
using PharmacySystem.Services.Auth;

namespace PharmacySystem.Data;

public class PharmacyDbContext : DbContext
{
    private readonly ICurrentUserService? _currentUserService;

    public PharmacyDbContext(DbContextOptions<PharmacyDbContext> options, ICurrentUserService? currentUserService = null) 
        : base(options)
    {
        _currentUserService = currentUserService;
    }

    public DbSet<Pharmacy> Pharmacies => Set<Pharmacy>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<Batch> Batches => Set<Batch>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<Purchase> Purchases => Set<Purchase>();
    public DbSet<PurchaseItem> PurchaseItems => Set<PurchaseItem>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Sale> Sales => Set<Sale>();
    public DbSet<SaleItem> SaleItems => Set<SaleItem>();
    public DbSet<SaleItemBatchAllocation> SaleItemBatchAllocations => Set<SaleItemBatchAllocation>();
    public DbSet<SalesReturn> SalesReturns => Set<SalesReturn>();
    public DbSet<SalesReturnItem> SalesReturnItems => Set<SalesReturnItem>();
    public DbSet<PurchaseReturn> PurchaseReturns => Set<PurchaseReturn>();
    public DbSet<PurchaseReturnItem> PurchaseReturnItems => Set<PurchaseReturnItem>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

    public int? CurrentPharmacyId => _currentUserService?.PharmacyId;
    public bool IsDeveloper => _currentUserService?.IsDeveloper ?? false;
    public bool IsAuthenticated => _currentUserService?.IsAuthenticated ?? false;
    public bool HasUserContext => _currentUserService != null;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PharmacyDbContext).Assembly);

        // Multi-Tenant Global Query Filters (Fails Closed for Ordinary Web Requests)
        // 1. If HasUserContext is false (DbInitializer, background tasks, tests without user context): allow execution
        // 2. If IsDeveloper is true: allow global administrative visibility across all pharmacies
        // 3. If IsAuthenticated is true with valid CurrentPharmacyId: strictly isolate to CurrentPharmacyId
        // 4. Otherwise (unauthenticated or missing pharmacy id): fails closed (0 records)
        modelBuilder.Entity<Product>().HasQueryFilter(e => 
            !HasUserContext || 
            IsDeveloper || 
            (IsAuthenticated && CurrentPharmacyId != null && e.PharmacyId == CurrentPharmacyId));

        modelBuilder.Entity<Category>().HasQueryFilter(e => 
            !HasUserContext || 
            IsDeveloper || 
            (IsAuthenticated && CurrentPharmacyId != null && e.PharmacyId == CurrentPharmacyId));

        modelBuilder.Entity<Location>().HasQueryFilter(e => 
            !HasUserContext || 
            IsDeveloper || 
            (IsAuthenticated && CurrentPharmacyId != null && e.PharmacyId == CurrentPharmacyId));

        modelBuilder.Entity<Supplier>().HasQueryFilter(e => 
            !HasUserContext || 
            IsDeveloper || 
            (IsAuthenticated && CurrentPharmacyId != null && e.PharmacyId == CurrentPharmacyId));

        modelBuilder.Entity<Customer>().HasQueryFilter(e => 
            !HasUserContext || 
            IsDeveloper || 
            (IsAuthenticated && CurrentPharmacyId != null && e.PharmacyId == CurrentPharmacyId));

        modelBuilder.Entity<Sale>().HasQueryFilter(e => 
            !HasUserContext || 
            IsDeveloper || 
            (IsAuthenticated && CurrentPharmacyId != null && e.PharmacyId == CurrentPharmacyId));

        modelBuilder.Entity<Purchase>().HasQueryFilter(e => 
            !HasUserContext || 
            IsDeveloper || 
            (IsAuthenticated && CurrentPharmacyId != null && e.PharmacyId == CurrentPharmacyId));

        modelBuilder.Entity<Expense>().HasQueryFilter(e => 
            !HasUserContext || 
            IsDeveloper || 
            (IsAuthenticated && CurrentPharmacyId != null && e.PharmacyId == CurrentPharmacyId));

        modelBuilder.Entity<AuditLog>().HasQueryFilter(e => 
            !HasUserContext || 
            IsDeveloper || 
            (IsAuthenticated && CurrentPharmacyId != null && e.PharmacyId == CurrentPharmacyId));

        modelBuilder.Entity<Batch>().HasQueryFilter(b => 
            !HasUserContext || 
            IsDeveloper || 
            (IsAuthenticated && CurrentPharmacyId != null && b.Product.PharmacyId == CurrentPharmacyId));

        modelBuilder.Entity<User>().HasQueryFilter(u => 
            !HasUserContext || 
            IsDeveloper || 
            (IsAuthenticated && CurrentPharmacyId != null && u.PharmacyId == CurrentPharmacyId));
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyTenantToAddedEntities();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        ApplyTenantToAddedEntities();
        return base.SaveChanges();
    }

    private void ApplyTenantToAddedEntities()
    {
        var tenantId = CurrentPharmacyId;

        foreach (var entry in ChangeTracker.Entries<ITenantEntity>())
        {
            if (entry.State == EntityState.Added && entry.Entity.PharmacyId <= 0)
            {
                if (tenantId.HasValue && tenantId.Value > 0)
                {
                    entry.Entity.PharmacyId = tenantId.Value;
                }
                else if (entry.Entity is AuditLog auditLog && auditLog.UserId.HasValue)
                {
                    var attachedUser = ChangeTracker.Entries<User>()
                        .FirstOrDefault(u => u.Entity.Id == auditLog.UserId.Value)?.Entity;
                    entry.Entity.PharmacyId = attachedUser?.PharmacyId ?? 1;
                }
                else
                {
                    // Safe fallback to default pharmacy to guarantee foreign key integrity
                    entry.Entity.PharmacyId = 1;
                }
            }
        }
    }
}
