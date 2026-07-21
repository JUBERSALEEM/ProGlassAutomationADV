using Microsoft.EntityFrameworkCore;
using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;
using System;
using System.IO;

namespace ProGlassAutomation.Services.ProformaInvoice.ProformaInvoiceRepository
{
    /// <summary>
    /// Dedicated SQLite DbContext for saved proforma invoices.
    /// Database file: %AppData%\ProGlassAutomation\proformainvoices.db
    ///
    /// Isolated from main proglass.db and specifications.db so invoices
    /// can be backed up, exported, or reset independently.
    /// </summary>
    public class ProformaInvoicesDbContext : DbContext
    {
        public DbSet<SavedProformaInvoice> ProformaInvoices { get; set; }

        public static string DatabaseFilePath
        {
            get
            {
                var appDataFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "ProGlassAutomation");

                if (!Directory.Exists(appDataFolder))
                    Directory.CreateDirectory(appDataFolder);

                return Path.Combine(appDataFolder, "proformainvoices.db");
            }
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite($"Data Source={DatabaseFilePath}");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SavedProformaInvoice>(entity =>
            {
                // Enforce ProformaId uniqueness (Phase 7.15 Q3 = Option A + C hybrid)
                entity.HasIndex(e => e.ProformaId).IsUnique();

                // Additional indexes for Load List UI (Phase 7.17)
                entity.HasIndex(e => e.CompanyName);
                entity.HasIndex(e => e.CustomerName);
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.CreatedAt);
            });

            base.OnModelCreating(modelBuilder);
        }
    }
}