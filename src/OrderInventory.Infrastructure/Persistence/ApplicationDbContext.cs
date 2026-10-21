using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using OrderInventory.Application.Interfaces;
using OrderInventory.Domain.Entities;
using OrderInventory.Domain.Enums;

namespace OrderInventory.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext, IApplicationDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
        {
            return Task.FromResult<IDbContextTransaction>(new InMemoryDbTransaction());
        }
        return Database.BeginTransactionAsync(cancellationToken);
    }

    public IExecutionStrategy CreateExecutionStrategy()
    {
        return Database.CreateExecutionStrategy();
    }

    private class InMemoryDbTransaction : IDbContextTransaction
    {
        public Guid TransactionId { get; } = Guid.NewGuid();
        public void Commit() { }
        public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Rollback() { }
        public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User Configuration
        modelBuilder.Entity<User>(builder =>
        {
            builder.HasKey(u => u.Id);
            builder.Property(u => u.FullName).IsRequired().HasMaxLength(150);
            builder.Property(u => u.Email).IsRequired().HasMaxLength(200);
            builder.HasIndex(u => u.Email).IsUnique();
            builder.Property(u => u.PasswordHash).IsRequired();
            builder.Property(u => u.Role).HasConversion<string>().HasMaxLength(20);
        });

        // Product Configuration
        modelBuilder.Entity<Product>(builder =>
        {
            builder.HasKey(p => p.Id);
            builder.Property(p => p.Sku).IsRequired().HasMaxLength(50);
            builder.HasIndex(p => p.Sku).IsUnique();
            builder.Property(p => p.Name).IsRequired().HasMaxLength(200);
            builder.Property(p => p.Price).HasPrecision(18, 2);
            builder.Property(p => p.StockQuantity).IsRequired();
            builder.Property(p => p.IsActive).HasDefaultValue(true);

            // Optimistic Concurrency via RowVersion / Timestamp
            builder.Property(p => p.RowVersion).IsRowVersion();
        });

        // Order Configuration
        modelBuilder.Entity<Order>(builder =>
        {
            builder.HasKey(o => o.Id);
            builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);
            builder.Property(o => o.SubTotal).HasPrecision(18, 2);
            builder.Property(o => o.Discount).HasPrecision(18, 2);
            builder.Property(o => o.Total).HasPrecision(18, 2);
            builder.Property(o => o.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
            builder.Property(o => o.RowVersion).IsRowVersion();

            builder.HasOne(o => o.Customer)
                .WithMany(u => u.Orders)
                .HasForeignKey(o => o.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // OrderItem Configuration
        modelBuilder.Entity<OrderItem>(builder =>
        {
            builder.HasKey(oi => oi.Id);
            builder.Property(oi => oi.Quantity).IsRequired();
            builder.Property(oi => oi.UnitPrice).HasPrecision(18, 2);

            builder.HasOne(oi => oi.Order)
                .WithMany(o => o.OrderItems)
                .HasForeignKey(oi => oi.OrderId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(oi => oi.Product)
                .WithMany(p => p.OrderItems)
                .HasForeignKey(oi => oi.ProductId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // IdempotencyRecord Configuration
        modelBuilder.Entity<IdempotencyRecord>(builder =>
        {
            builder.HasKey(r => r.Id);
            builder.Property(r => r.Key).IsRequired().HasMaxLength(128);
            builder.Property(r => r.RequestHash).IsRequired().HasMaxLength(64);
            builder.HasIndex(r => new { r.CustomerId, r.Key }).IsUnique();
            builder.Property(r => r.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
        });

        // OutboxMessage Configuration
        modelBuilder.Entity<OutboxMessage>(builder =>
        {
            builder.HasKey(m => m.Id);
            builder.Property(m => m.EventType).IsRequired().HasMaxLength(100);
            builder.Property(m => m.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
        });
    }
}
