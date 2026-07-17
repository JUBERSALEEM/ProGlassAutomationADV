using Microsoft.EntityFrameworkCore;
using ProGlassAutomation.Models.SheetStore;
using ProGlassAutomation.Models.SheetStore.SheetStoreHistory;
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
        public DbSet<SheetHistory> SheetHistories { get; set; }

        // Future modules add DbSets here:
        // public DbSet<ProformaInvoice> ProformaInvoices { get; set; }

        private static readonly string DbFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ProGlassAutomation");

        private static readonly string DbPath = Path.Combine(DbFolder, "proglass.db");

        public static string DatabaseFilePath => DbPath;

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            if (!Directory.Exists(DbFolder))
                Directory.CreateDirectory(DbFolder);

            options.UseSqlite($"Data Source={DbPath}");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // GlassSheet unique constraint
            modelBuilder.Entity<GlassSheet>()
                .HasIndex(g => new { g.Category, g.ThicknessMm, g.Color, g.WidthMm, g.HeightMm, g.Supplier })
                .IsUnique()
                .HasDatabaseName("IX_GlassSheet_Unique");

            modelBuilder.Entity<GlassSheet>()
                .Property(g => g.PurchasePricePerSqm)
                .HasPrecision(18, 2);

            modelBuilder.Entity<GlassSheet>()
                .Property(g => g.SellPricePerSqm)
                .HasPrecision(18, 2);

            // SheetHistory indexes for fast lookup
            modelBuilder.Entity<SheetHistory>()
                .HasIndex(h => h.SheetId)
                .HasDatabaseName("IX_SheetHistory_SheetId");

            modelBuilder.Entity<SheetHistory>()
                .HasIndex(h => h.Timestamp)
                .HasDatabaseName("IX_SheetHistory_Timestamp");

            modelBuilder.Entity<SheetHistory>()
                .Property(h => h.ValueChanged)
                .HasPrecision(18, 2);
        }
    }
}