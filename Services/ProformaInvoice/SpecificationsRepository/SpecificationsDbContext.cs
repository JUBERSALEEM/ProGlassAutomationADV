using Microsoft.EntityFrameworkCore;
using ProGlassAutomation.Models.ProformaInvoice.Specifications;
using System;
using System.IO;

namespace ProGlassAutomation.Services.ProformaInvoice.SpecificationsRepository
{
    /// <summary>
    /// Separate SQLite database context for saved specifications.
    /// Stores all glass specs generated in Step 4 for reuse in Step 5.
    ///
    /// Database file: %AppData%\ProGlassAutomation\specifications.db
    /// Isolated from main proglass.db to keep spec library portable
    /// (can be exported/imported/shared between installations).
    /// </summary>
    public class SpecificationsDbContext : DbContext
    {
        public DbSet<SavedSpecification> Specifications { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            var appDataPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ProGlassAutomation");

            // Ensure folder exists
            if (!Directory.Exists(appDataPath))
                Directory.CreateDirectory(appDataPath);

            var dbPath = Path.Combine(appDataPath, "specifications.db");

            optionsBuilder.UseSqlite($"Data Source={dbPath}");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SavedSpecification>(entity =>
            {
                entity.HasKey(s => s.Id);

                entity.Property(s => s.GlassType)
                      .IsRequired()
                      .HasMaxLength(20);

                entity.Property(s => s.SpecName)
                      .IsRequired()
                      .HasMaxLength(500);

                // D-6: TEXT affinity for exact decimal storage (SQLite has no
                // native decimal; "decimal(18,2)" gives NUMERIC -> REAL float
                // storage, losing precision under SQL-side SUM). TEXT matches
                // the rest of the codebase. EnsureCreated means existing
                // databases are untouched; new ones get the corrected column.
                entity.Property(s => s.PricePerSqm)
                      .HasColumnType("TEXT");

                entity.Property(s => s.CreatedAt)
                      .IsRequired();

                entity.Property(s => s.LastUsedAt)
                      .IsRequired();

                // Index for fast filtering by GlassType (SGU/DGU/LAM lookups)
                entity.HasIndex(s => s.GlassType);

                // Index for spec name searches
                entity.HasIndex(s => s.SpecName);
            });
        }
    }
}