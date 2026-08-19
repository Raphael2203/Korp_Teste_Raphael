using BillingService.Models;
using Microsoft.EntityFrameworkCore;

namespace BillingService.Data;

public class BillingDbContext : DbContext
{
    public const string InvoiceNumberSequence = "invoice_number_seq";

    public BillingDbContext(
        DbContextOptions<BillingDbContext> options)
        : base(options)
    {
    }

    public DbSet<Invoice> Invoices => Set<Invoice>();

    public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // A numeração sequencial fica a cargo do banco: mesmo com várias notas
        // sendo criadas ao mesmo tempo, cada uma recebe um número distinto.
        modelBuilder.HasSequence<int>(InvoiceNumberSequence)
            .StartsAt(1)
            .IncrementsBy(1);

        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.HasKey(i => i.Id);

            entity.Property(i => i.Number)
                .IsRequired()
                .HasDefaultValueSql($"nextval('\"{InvoiceNumberSequence}\"')")
                .ValueGeneratedOnAdd();

            entity.HasIndex(i => i.Number)
                .IsUnique();

            entity.Property(i => i.Status)
                .IsRequired()
                .HasMaxLength(20)
                .HasConversion<string>();

            entity.Property(i => i.CreatedAt)
                .IsRequired();

            entity.HasMany(i => i.Items)
                .WithOne(item => item.Invoice)
                .HasForeignKey(item => item.InvoiceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<InvoiceItem>(entity =>
        {
            entity.HasKey(item => item.Id);

            entity.Property(item => item.ProductId)
                .IsRequired();

            entity.Property(item => item.ProductCode)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(item => item.ProductDescription)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(item => item.Quantity)
                .IsRequired();
        });
    }
}
