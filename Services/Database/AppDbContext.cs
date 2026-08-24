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

        public static string DatabaseFilePath =>
            Path.Combine(AppDataRoot.CurrentFolder, "proglass.db");

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            Directory.CreateDirectory(AppDataRoot.CurrentFolder);
            // Senior audit (Aug 2026): route through SqliteResilience for WAL,
            // busy_timeout=5s, foreign_keys=ON. See SqliteResilience.cs.
            SqliteResilience.ConfigureFor(options, DatabaseFilePath);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // GlassSheet unique constraint
            modelBuilder.Entity<GlassSheet>()
                .HasIndex(g => new { g.Category, g.ThicknessMm, g.Color, g.WidthMm, g.HeightMm, g.Supplier })
                .IsUnique()
                .HasDatabaseName("IX_GlassSheet_Unique");

            // D-6: money columns must use TEXT affinity (exact decimal
            // storage), matching the rest of the codebase. HasPrecision(18,2)
            // on SQLite yields NUMERIC affinity -> REAL (binary float)
            // storage, which loses precision under SQL-side SUM. The app
            // uses EnsureCreated, so existing databases are untouched; only
            // newly-created databases get the corrected TEXT columns.
            modelBuilder.Entity<GlassSheet>()
                .Property(g => g.PurchasePricePerSqm)
                .HasColumnType("TEXT");

            modelBuilder.Entity<GlassSheet>()
                .Property(g => g.SellPricePerSqm)
                .HasColumnType("TEXT");

            // SheetHistory indexes for fast lookup
            modelBuilder.Entity<SheetHistory>()
                .HasIndex(h => h.SheetId)
                .HasDatabaseName("IX_SheetHistory_SheetId");

            modelBuilder.Entity<SheetHistory>()
                .HasIndex(h => h.Timestamp)
                .HasDatabaseName("IX_SheetHistory_Timestamp");

            // D-6: TEXT affinity for exact decimal storage (see GlassSheet above).
            modelBuilder.Entity<SheetHistory>()
                .Property(h => h.ValueChanged)
                .HasColumnType("TEXT");
        }
    }
}