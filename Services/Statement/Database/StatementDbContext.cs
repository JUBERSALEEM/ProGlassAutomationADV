// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Statement/Database/StatementDbContext.cs
// PROJECT: ProGlassAutomation
// MODULE: Account Statement / Sales Report (Chat 16)
//
// FIX vs PHASE 2 VERSION:
//   HasOne/WithMany relationship now correctly references
//   SavedStatement.LineItems navigation property (which now exists).
//   All other configuration UNCHANGED.
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.EntityFrameworkCore;
using ProGlassAutomation.Models.Statement;
using System;
using System.IO;

namespace ProGlassAutomation.Services.Statement.Database
{
    public class StatementDbContext : DbContext
    {
        // ── Tables ────────────────────────────────────────────────────────
        public DbSet<SavedStatement> Statements { get; set; } = null!;
        public DbSet<StatementLineItem> LineItems { get; set; } = null!;

        // ── DB file path ──────────────────────────────────────────────────
        // Pure path computation — no side effects (2026-09-24): folder
        // creation lives in OnConfiguring via AppDataRoot.EnsureCurrentFolder.
        private static string DbPath => Path.Combine(
            ProGlassAutomation.Services.Database.AppDataRoot.CurrentFolder,
            "statements.db");

        /// <summary>Public accessor used by the schema migration runner.</summary>
        public static string DatabaseFilePath => DbPath;

        // ── Configuration ─────────────────────────────────────────────────
        protected override void OnConfiguring(
            DbContextOptionsBuilder optionsBuilder)
        {
            // Shared-aware folder ensure (2026-09-24) — see AppDataRoot.
            ProGlassAutomation.Services.Database.AppDataRoot.EnsureCurrentFolder();
            // Senior audit (Aug 2026): WAL + busy_timeout=5s + foreign_keys=ON
            // via SqliteResilience. Statement generation reads across five
            // sibling databases in parallel — without WAL, one long read
            // would block PI/JO/DN/TI writers.
            ProGlassAutomation.Services.Database.SqliteResilience
                .ConfigureFor(optionsBuilder, DbPath);
            optionsBuilder.EnableSensitiveDataLogging(false);
        }

        // ── Model / fluent API ────────────────────────────────────────────
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // ── SavedStatement ────────────────────────────────────────────
            modelBuilder.Entity<SavedStatement>(entity =>
            {
                entity.Property(e => e.RowVersion)
                    .IsConcurrencyToken()
                    .ValueGeneratedNever();

                // Unique statement number (STMT-YYYY-NNN)
                entity.HasIndex(e => e.StatementNumber)
                      .IsUnique();

                // Fast list-page queries — indexed filter columns
                entity.HasIndex(e => e.FilterCustomer);
                entity.HasIndex(e => e.FilterCompany);
                entity.HasIndex(e => e.FilterProject);
                entity.HasIndex(e => e.FilterSalesman);
                entity.HasIndex(e => e.CreatedAt);

                // Decimal columns stored as TEXT (avoid SQLite REAL rounding)
                entity.Property(e => e.TotalInvoiced)
                      .HasColumnType("TEXT");
                entity.Property(e => e.TotalOutstanding)
                      .HasColumnType("TEXT");
                entity.Property(e => e.OverdueAmount)
                      .HasColumnType("TEXT");

                // JSON blobs stored as TEXT
                entity.Property(e => e.FullSnapshotJson)
                      .HasColumnType("TEXT");
                entity.Property(e => e.FilterJson)
                      .HasColumnType("TEXT");
            });

            // ── StatementLineItem ─────────────────────────────────────────
            modelBuilder.Entity<StatementLineItem>(entity =>
            {
                // Index for parent lookup
                entity.HasIndex(e => e.StatementId);

                // Composite unique: one invoice row per statement
                entity.HasIndex(e => new { e.StatementId, e.InvoiceNumber })
                      .IsUnique();

                // Cascade delete: removing statement removes its lines
                // NOW correctly references SavedStatement.LineItems nav prop
                entity.HasOne(e => e.Statement)
                      .WithMany(s => s.LineItems)
                      .HasForeignKey(e => e.StatementId)
                      .OnDelete(DeleteBehavior.Cascade);

                // Decimal columns as TEXT (SQLite REAL rounding prevention)
                entity.Property(e => e.TotalSqm)
                      .HasColumnType("TEXT");
                entity.Property(e => e.Subtotal)
                      .HasColumnType("TEXT");
                entity.Property(e => e.VatAmount)
                      .HasColumnType("TEXT");
                entity.Property(e => e.GrandTotal)
                      .HasColumnType("TEXT");
                entity.Property(e => e.PaidAmount)
                      .HasColumnType("TEXT");
            });

            base.OnModelCreating(modelBuilder);
        }
    }
}