using InventoryService.Models;
using Microsoft.EntityFrameworkCore;

namespace InventoryService.Data;

public class InventoryDbContext : DbContext
{
    public InventoryDbContext(
        DbContextOptions<InventoryDbContext> options)
        : base(options)
    {
    }

    public DbSet<Product> Products => Set<Product>();

    public DbSet<StockOperation> StockOperations => Set<StockOperation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(p => p.Id);

            entity.Property(p => p.Code)
                .IsRequired()
                .HasMaxLength(50);

            entity.HasIndex(p => p.Code)
                .IsUnique();

            entity.Property(p => p.Description)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(p => p.Stock)
                .IsRequired();
        });

        modelBuilder.Entity<StockOperation>(entity =>
        {
            entity.HasKey(o => o.Id);

            entity.Property(o => o.OperationKey)
                .IsRequired()
                .HasMaxLength(100);

            // Impede que a mesma operação seja aplicada duas vezes.
            entity.HasIndex(o => o.OperationKey)
                .IsUnique();

            entity.Property(o => o.CreatedAt)
                .IsRequired();
        });
    }
}
