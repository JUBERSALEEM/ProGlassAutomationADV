using Microsoft.EntityFrameworkCore;
using ProGlassAutomation.Models.DeliveryNote.SavedDeliveryNote;
using System;
using System.IO;

namespace ProGlassAutomation.Services.DeliveryNote.Database
{
    /// <summary>
    /// EF Core DbContext for the 5th SQLite database: deliverynotes.db
    ///
    /// Location: %AppData%\ProGlassAutomation\deliverynotes.db
    ///
    /// Tables:
    ///   DeliveryNotes           — main DN header + JSON snapshot
    ///   DeliveryNoteItems       — normalized line items per DN
    ///   DeliveryNoteAttachments — file references (images + PDFs)
    ///
    /// Initialized in App.xaml.cs alongside other DbContexts:
    ///   App.DeliveryNotes = new DeliveryNoteRepository();
    ///   await App.DeliveryNotes.InitializeAsync();
    /// </summary>
    public class DeliveryNotesDbContext : DbContext
    {
        public DbSet<SavedDeliveryNote> DeliveryNotes { get; set; }
        public DbSet<DeliveryNoteItem> DeliveryNoteItems { get; set; }
        public DbSet<DeliveryNoteAttachment> DeliveryNoteAttachments { get; set; }

        private static string DbPath
        {
            get
            {
                var folder = Path.Combine(
                    Environment.GetFolderPath(
                        Environment.SpecialFolder.ApplicationData),
                    "ProGlassAutomation");

                if (!Directory.Exists(folder))
                    Directory.CreateDirectory(folder);

                return Path.Combine(folder, "deliverynotes.db");
            }
        }

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            options.UseSqlite($"Data Source={DbPath}");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // ── DeliveryNotes table ──────────────────────────────────────
            modelBuilder.Entity<SavedDeliveryNote>(e =>
            {
                e.HasIndex(x => x.DeliveryNoteId).IsUnique();
                e.HasIndex(x => x.JobOrderId);
                e.HasIndex(x => x.ProformaId);
                e.HasIndex(x => x.CustomerName);
                e.HasIndex(x => x.Status);
                e.HasIndex(x => x.DeliveryType);
                e.HasIndex(x => x.DeliveryDate);

                // One-to-many: DN → Items
                e.HasMany(x => x.Items)
                    .WithOne()
                    .HasForeignKey(i => i.DeliveryNoteId)
                    .HasPrincipalKey(x => x.DeliveryNoteId)
                    .OnDelete(DeleteBehavior.Cascade);

                // One-to-many: DN → Attachments
                e.HasMany(x => x.Attachments)
                    .WithOne()
                    .HasForeignKey(a => a.DeliveryNoteId)
                    .HasPrincipalKey(x => x.DeliveryNoteId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // ── DeliveryNoteItems table ──────────────────────────────────
            modelBuilder.Entity<DeliveryNoteItem>(e =>
            {
                e.HasIndex(x => x.DeliveryNoteId);
                e.HasIndex(x => x.JobOrderId);
                e.HasIndex(x => new { x.JobOrderId, x.SpecGroupNumber, x.SerialNumber });
            });

            // ── DeliveryNoteAttachments table ────────────────────────────
            modelBuilder.Entity<DeliveryNoteAttachment>(e =>
            {
                e.HasIndex(x => x.DeliveryNoteId);
            });
        }
    }
}