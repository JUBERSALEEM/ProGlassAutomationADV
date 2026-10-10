using Microsoft.EntityFrameworkCore;
using ProGlassAutomation.Models.CreditNote.SavedCreditNote;
using System.IO;

namespace ProGlassAutomation.Services.CreditNote.Database
{
    /// <summary>
    /// EF Core DbContext for creditnotes.db (ERP hardening — Phase 4).
    ///
    /// Location: %AppData%\ProGlassAutomation\creditnotes.db
    ///
    /// Tables:
    ///   CreditNotes     — header + JSON snapshot
    ///   CreditNoteItems — normalized line items
    ///
    /// Patterns carried verbatim from TaxInvoiceDbContext:
    ///   • DbPath pure computation — no side effects in getter
    ///   • Directory creation in OnConfiguring only
    ///   • HasPrincipalKey on the string FK (CreditNoteNumber)
    ///   • Cascade delete on Items
    ///   • Explicit TEXT column type on decimals + FullSnapshotJson
    ///   • Indexes on all searchable + filterable columns
    ///   • WAL + busy_timeout + foreign_keys=ON via SqliteResilience
    /// </summary>
    public class CreditNoteDbContext : DbContext
    {
        public DbSet<SavedCreditNote> CreditNotes { get; set; } = null!;
        public DbSet<CreditNoteItem> CreditNoteItems { get; set; } = null!;

        private static string DbPath =>
            Path.Combine(
                ProGlassAutomation.Services.Database.AppDataRoot.CurrentFolder,
                "creditnotes.db");

        /// <summary>Public accessor used by the schema migration runner.</summary>
        public static string DatabaseFilePath => DbPath;

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            // Shared-aware folder ensure (2026-09-24) — see AppDataRoot.
            ProGlassAutomation.Services.Database.AppDataRoot.EnsureCurrentFolder();

            ProGlassAutomation.Services.Database.SqliteResilience
                .ConfigureFor(options, DbPath);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<SavedCreditNote>(e =>
            {
                e.Property(x => x.RowVersion)
                    .IsConcurrencyToken()
                    .ValueGeneratedNever();
                e.HasIndex(x => x.CreditNoteNumber).IsUnique();

                e.HasIndex(x => x.TaxInvoiceReference)
                    .HasDatabaseName("IX_CreditNotes_TaxInvoiceReference");
                e.HasIndex(x => x.ProformaId)
                    .HasDatabaseName("IX_CreditNotes_ProformaId");
                e.HasIndex(x => x.JobOrderId)
                    .HasDatabaseName("IX_CreditNotes_JobOrderId");
                e.HasIndex(x => x.CustomerName)
                    .HasDatabaseName("IX_CreditNotes_CustomerName");
                e.HasIndex(x => x.Status)
                    .HasDatabaseName("IX_CreditNotes_Status");
                e.HasIndex(x => x.IssueDate)
                    .HasDatabaseName("IX_CreditNotes_IssueDate");
                e.HasIndex(x => x.IsVoid)
                    .HasDatabaseName("IX_CreditNotes_IsVoid");

                e.Property(x => x.FullSnapshotJson).HasColumnType("TEXT");
                e.Property(x => x.Subtotal).HasColumnType("TEXT");
                e.Property(x => x.DiscountAmount).HasColumnType("TEXT");
                e.Property(x => x.AfterDiscount).HasColumnType("TEXT");
                e.Property(x => x.VatAmount).HasColumnType("TEXT");
                e.Property(x => x.GrandTotal).HasColumnType("TEXT");
                e.Property(x => x.TotalSqm).HasColumnType("TEXT");

                e.HasMany(x => x.Items)
                    .WithOne(i => i.CreditNote)
                    .HasForeignKey(i => i.CreditNoteNumber)
                    .HasPrincipalKey(x => x.CreditNoteNumber)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<CreditNoteItem>(e =>
            {
                e.HasIndex(x => x.CreditNoteNumber)
                    .HasDatabaseName("IX_CreditNoteItems_CreditNoteNumber");
                e.HasIndex(x => x.ProformaId)
                    .HasDatabaseName("IX_CreditNoteItems_ProformaId");

                e.Property(x => x.Sqm).HasColumnType("TEXT");
                e.Property(x => x.TotalSqm).HasColumnType("TEXT");
                e.Property(x => x.Rate).HasColumnType("TEXT");
                e.Property(x => x.Value).HasColumnType("TEXT");
            });
        }
    }
}
