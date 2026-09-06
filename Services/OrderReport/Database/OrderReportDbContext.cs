// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/OrderReport/Database/OrderReportDbContext.cs
// PROJECT: ProGlassAutomation
// MODULE: Report Generator — persistence (2026-09-06)
//
// Mirrors Services/Statement/Database/StatementDbContext.cs: its own
// SQLite file (orderreports.db) under AppDataRoot.CurrentFolder, WAL +
// busy_timeout via SqliteResilience, and TEXT-typed decimal columns so
// SQLite REAL rounding cannot corrupt money/weight values.
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.EntityFrameworkCore;
using ProGlassAutomation.Models.OrderReport;
using System.IO;

namespace ProGlassAutomation.Services.OrderReport.Database
{
    public class OrderReportDbContext : DbContext
    {
        public DbSet<SavedOrderReport> OrderReports { get; set; } = null!;

        private static string DbPath
        {
            get
            {
                Directory.CreateDirectory(
                    ProGlassAutomation.Services.Database.AppDataRoot.CurrentFolder);
                return Path.Combine(
                    ProGlassAutomation.Services.Database.AppDataRoot.CurrentFolder,
                    "orderreports.db");
            }
        }

        public static string DatabaseFilePath => DbPath;

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            ProGlassAutomation.Services.Database.SqliteResilience
                .ConfigureFor(optionsBuilder, DbPath);
            optionsBuilder.EnableSensitiveDataLogging(false);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SavedOrderReport>(entity =>
            {
                entity.HasIndex(e => e.ReportNumber).IsUnique();
                entity.HasIndex(e => e.FilterCustomer);
                entity.HasIndex(e => e.FilterCompany);
                entity.HasIndex(e => e.FilterProject);
                entity.HasIndex(e => e.FilterSalesman);
                entity.HasIndex(e => e.CreatedAt);

                entity.Property(e => e.TotalInvoicedValue).HasColumnType("TEXT");
                entity.Property(e => e.FullSnapshotJson).HasColumnType("TEXT");
                entity.Property(e => e.FilterJson).HasColumnType("TEXT");
            });

            base.OnModelCreating(modelBuilder);
        }
    }
}
