using Microsoft.EntityFrameworkCore;
using ProGlassAutomation.Models.TaxInvoice;
using System;
using System.IO;

namespace ProGlassAutomation.Services.TaxInvoice.Database
{
    /// <summary>
    /// EF Core DbContext for the 6th SQLite database: taxinvoices.db
    /// Location: %AppData%\ProGlassAutomation\taxinvoices.db
    /// </summary>
    public class TaxInvoiceDbContext : DbContext
    {
        public DbSet<Models.TaxInvoice.TaxInvoice> TaxInvoices { get; set; }
        public DbSet<TaxInvoiceItem> TaxInvoiceItems { get; set; }
        public DbSet<TaxInvoiceHistory> TaxInvoiceHistories { get; set; }

        private static string DbPath
        {
            get
            {
                var folder = Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.ApplicationData),
                    "ProGlassAutomation");

                return Path.Combine(folder, "taxinvoices.db");
            }
        }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            var folder = Path.GetDirectoryName(DbPath)!;
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            options.UseSqlite($"Data Source={DbPath}");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Models.TaxInvoice.TaxInvoice>(e =>
            {
                e.HasIndex(x => x.InvoiceNumber).IsUnique();
                e.HasIndex(x => x.DeliveryNoteId);
                e.HasIndex(x => x.JobOrderId);
                e.HasIndex(x => x.ProformaId);
                e.HasIndex(x => x.CustomerName);
                e.HasIndex(x => x.Salesman);
                e.HasIndex(x => x.InvoiceDate);

                e.Property(x => x.FullSnapshotJson)
                    .HasColumnType("TEXT");

                // One-to-many: Invoice → Items (cascade delete)
                e.HasMany(x => x.Items)
                    .WithOne(i => i.TaxInvoice)
                    .HasForeignKey(i => i.TaxInvoiceId)
                    .HasPrincipalKey(x => x.InvoiceNumber)
                    .OnDelete(DeleteBehavior.Cascade);

                // One-to-many: Invoice → History (cascade delete)
                e.HasMany(x => x.History)
                    .WithOne(h => h.TaxInvoice)
                    .HasForeignKey(h => h.TaxInvoiceId)
                    .HasPrincipalKey(x => x.InvoiceNumber)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<TaxInvoiceItem>(e =>
            {
                e.HasIndex(x => x.TaxInvoiceId);
            });

            modelBuilder.Entity<TaxInvoiceHistory>(e =>
            {
                e.HasIndex(x => x.TaxInvoiceId);
            });
        }
    }
}