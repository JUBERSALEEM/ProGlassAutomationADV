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

        /// <summary>
        /// Non-glass inventory (spacer, sealant, desiccant, cavity gas,
        /// hardware) — see InventoryItem for why these live in their own
        /// table rather than on GlassSheets.
        /// </summary>
        public DbSet<InventoryItem> InventoryItems { get; set; }

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

            // ── Inventory consumables ────────────────────────────────
            // Same D-6 discipline: every decimal is TEXT affinity, and
            // here it covers QUANTITIES too, not just money. Spacer bar
            // is stocked in linear metres (45.55 LM), so a REAL column
            // would accumulate binary-float drift across issues and
            // receipts until the balance no longer matched the shelf.
            foreach (var prop in new[]
                     {
                         nameof(InventoryItem.Stock),
                         nameof(InventoryItem.Used),
                         nameof(InventoryItem.ReorderLevel),
                         nameof(InventoryItem.PurchasePricePerUnit),
                         nameof(InventoryItem.SellPricePerUnit)
                     })
            {
                modelBuilder.Entity<InventoryItem>()
                    .Property(prop)
                    .HasColumnType("TEXT");
            }

            // Enums persist as INT (their default). Members are
            // append-only so stored values keep their meaning.
            modelBuilder.Entity<InventoryItem>()
                .HasIndex(i => i.Category)
                .HasDatabaseName("IX_InventoryItem_Category");

            modelBuilder.Entity<InventoryItem>()
                .HasIndex(i => i.Supplier)
                .HasDatabaseName("IX_InventoryItem_Supplier");
        }
    }
}