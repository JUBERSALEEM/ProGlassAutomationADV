using Microsoft.EntityFrameworkCore;
using ProGlassAutomation.Models.SheetStore;
using System;
using System.IO;

namespace ProGlassAutomation.Services.Database
{
    /// <summary>
    /// Single EF Core DbContext for the entire ProGlass Automation app.
    /// All modules add their DbSets here — one shared database file.
    /// Location: %AppData%\ProGlassAutomation\proglass.db
    /// </summary>
    public class AppDbContext : DbContext
    {
        // ============ TABLES ============
        public DbSet<GlassSheet> GlassSheets { get; set; }

        // Future modules add DbSets here:
        // public DbSet<ProformaInvoice> ProformaInvoices { get; set; }
        // public DbSet<JobOrder> JobOrders { get; set; }
        // etc.

        // ============ DATABASE FILE LOCATION ============
        private static readonly string DbFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ProGlassAutomation");

        private static readonly string DbPath = Path.Combine(DbFolder, "proglass.db");

        public static string DatabaseFilePath => DbPath;

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            // Ensure folder exists
            if (!Directory.Exists(DbFolder))
                Directory.CreateDirectory(DbFolder);

            options.UseSqlite($"Data Source={DbPath}");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure GlassSheet — no duplicate SKUs allowed
            modelBuilder.Entity<GlassSheet>()
                .HasIndex(g => new { g.Category, g.ThicknessMm, g.Color, g.WidthMm, g.HeightMm, g.Supplier })
                .IsUnique()
                .HasDatabaseName("IX_GlassSheet_Unique");

            // Decimal precision for pricing columns
            modelBuilder.Entity<GlassSheet>()
                .Property(g => g.PurchasePricePerSqm)
                .HasPrecision(18, 2);

            modelBuilder.Entity<GlassSheet>()
                .Property(g => g.SellPricePerSqm)
                .HasPrecision(18, 2);
        }
    }
}