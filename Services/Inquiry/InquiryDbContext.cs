using Microsoft.EntityFrameworkCore;
using ProGlassAutomation.Models.Inquiry;
using ProGlassAutomation.Services.Database;
using System.IO;

namespace ProGlassAutomation.Services.Inquiry
{
    public sealed class InquiryDbContext : DbContext
    {
        public DbSet<InquiryRecord> Inquiries => Set<InquiryRecord>();
        public DbSet<InquiryAttachment> Attachments => Set<InquiryAttachment>();
        public DbSet<InquiryCommunication> Communications => Set<InquiryCommunication>();
        public static string DatabaseFilePath => Path.Combine(AppDataRoot.CurrentFolder, "inquiries.db");

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(DatabaseFilePath)!);
            options.UseSqlite($"Data Source={DatabaseFilePath}");
        }

        protected override void OnModelCreating(ModelBuilder model)
        {
            model.Entity<InquiryRecord>(e =>
            {
                e.HasIndex(x => x.InquiryNumber).IsUnique();
                e.HasIndex(x => x.ReceivedAt); e.HasIndex(x => x.UpdatedAt);
                e.HasIndex(x => x.Status); e.HasIndex(x => x.CustomerId);
                e.HasIndex(x => x.SalesmanId); e.HasIndex(x => x.PreparedByUserId);
                e.HasMany(x => x.Attachments).WithOne(x => x.Inquiry)
                    .HasForeignKey(x => x.InquiryId).OnDelete(DeleteBehavior.Cascade);
                e.HasMany(x => x.Communications).WithOne(x => x.Inquiry)
                    .HasForeignKey(x => x.InquiryId).OnDelete(DeleteBehavior.Cascade);
            });
            model.Entity<InquiryAttachment>(e =>
            {
                e.HasIndex(x => x.InquiryId); e.HasIndex(x => x.UploadedAt);
            });
            model.Entity<InquiryCommunication>(e =>
            {
                e.HasIndex(x => new { x.InquiryId, x.OccurredAt });
            });
        }
    }
}
