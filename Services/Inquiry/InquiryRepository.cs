using Microsoft.EntityFrameworkCore;
using ProGlassAutomation.Models.Inquiry;
using ProGlassAutomation.Services.Database;
using ProGlassAutomation.Services.Database.SchemaMigrations;
using ProGlassAutomation.Services.Reporting;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.Inquiry
{
    public sealed class InquiryRepository : IInquiryRepository
    {
        public async Task InitializeAsync(CancellationToken ct = default)
        {
            await using var db = new InquiryDbContext();
            await db.Database.EnsureCreatedAsync(ct).ConfigureAwait(false);
            SchemaMigrationRunner.Upgrade(InquiryDbContext.DatabaseFilePath,
                AppSchemaMigrations.Inquiries, "Inquiry");
        }

        public async Task<PagedResult<InquiryListProjection>> QueryAsync(InquiryQuery query, CancellationToken ct = default)
        {
            await using var db = new InquiryDbContext();
            var source = ApplyFilters(db.Inquiries.AsNoTracking(), query, includeStatus: true);
            source = query.Sort switch
            {
                "Received oldest" => source.OrderBy(x => x.ReceivedAt),
                "Received newest" => source.OrderByDescending(x => x.ReceivedAt),
                "Inquiry number" => source.OrderBy(x => x.InquiryNumber),
                _ => source.OrderByDescending(x => x.UpdatedAt)
            };
            var page = await GlobalQueryEngine.Instance.ExecutePagedQueryAsync(source,
                x => new InquiryListProjection
                {
                    Id = x.Id, InquiryNumber = x.InquiryNumber, InquiryReference = x.InquiryReference,
                    CustomerName = x.CustomerName, CustomerLocation = x.CustomerLocation,
                    ProjectName = x.ProjectName, ProjectDescription = x.ProjectDescription,
                    SalesmanName = x.SalesmanName, PreparedByName = x.PreparedByName,
                    PreparedByRole = x.PreparedByRole, Status = x.Status, Priority = x.Priority,
                    InquiryValue = x.InquiryValue, ReceivedAt = x.ReceivedAt, UpdatedAt = x.UpdatedAt, UpdatedBy = x.UpdatedBy,
                    AttachmentCount = x.Attachments.Count, ProformaInvoiceId = x.ProformaInvoiceId,
                    ProformaNumber = x.ProformaNumber, JobOrderNumber = x.JobOrderNumber
                }, query.Page, Math.Clamp(query.PageSize, 10, 200), ct).ConfigureAwait(false);
            var ids = page.Items.Select(x => x.Id).ToList();
            if (ids.Count > 0)
            {
                var metadata = await db.Attachments.AsNoTracking()
                    .Where(x => ids.Contains(x.InquiryId))
                    .Select(x => new { x.InquiryId, x.Extension })
                    .ToListAsync(ct).ConfigureAwait(false);
                foreach (var row in page.Items)
                {
                    var types = metadata.Where(x => x.InquiryId == row.Id)
                        .Select(x => (x.Extension ?? "").TrimStart('.').ToUpperInvariant())
                        .Where(x => x.Length > 0).Distinct().Take(3).ToList();
                    int extra = Math.Max(0, row.AttachmentCount - types.Count);
                    row.AttachmentBadgeText = string.Join("  ", types)
                        + (extra > 0 ? $"  +{extra}" : "");
                }
            }
            return page;
        }

        public async Task<InquiryStatistics> GetStatisticsAsync(InquiryQuery query, CancellationToken ct = default)
        {
            await using var db = new InquiryDbContext();
            var source = ApplyFilters(db.Inquiries.AsNoTracking(), query, includeStatus: false);
            var counts = await source.GroupBy(x => x.Status)
                .Select(x => new { x.Key, Count = x.Count() }).ToListAsync(ct).ConfigureAwait(false);
            int Count(string status) => counts.FirstOrDefault(x => x.Key == status)?.Count ?? 0;
            return new InquiryStatistics
            {
                All = counts.Sum(x => x.Count), Received = Count(InquiryStatuses.Received),
                Sent = Count(InquiryStatuses.Sent), Confirmed = Count(InquiryStatuses.Confirmed),
                Hold = Count(InquiryStatuses.Hold), Cancelled = Count(InquiryStatuses.Cancelled)
            };
        }

        public async Task<InquiryRecord?> GetDetailAsync(int id, CancellationToken ct = default)
        {
            await using var db = new InquiryDbContext();
            return await db.Inquiries.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct).ConfigureAwait(false);
        }

        public async Task<InquiryRecord> CreateAsync(InquiryRecord inquiry, CancellationToken ct = default)
        {
            if (inquiry == null) throw new ArgumentNullException(nameof(inquiry));
            await using var db = new InquiryDbContext();
            if (string.IsNullOrWhiteSpace(inquiry.InquiryNumber))
            {
                string prefix = $"INQ-{DateTime.Now:yyyy}-";
                var ids = await db.Inquiries.AsNoTracking()
                    .Where(x => x.InquiryNumber.StartsWith(prefix))
                    .Select(x => x.InquiryNumber).ToListAsync(ct).ConfigureAwait(false);
                int next = ids.Select(x => int.TryParse(x.Substring(prefix.Length), out int n) ? n : 0)
                    .DefaultIfEmpty().Max() + 1;
                inquiry.InquiryNumber = prefix + next.ToString("D4");
            }
            var now = DateTimeOffset.Now;
            inquiry.Status = InquiryStatuses.Normalize(inquiry.Status);
            inquiry.CreatedAt = now; inquiry.UpdatedAt = now;
            if (inquiry.ReceivedAt == default) inquiry.ReceivedAt = now;
            db.Inquiries.Add(inquiry);
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            return inquiry;
        }

        public async Task<InquiryRecord> UpdateAsync(InquiryRecord inquiry, CancellationToken ct = default)
        {
            if (inquiry == null || inquiry.Id <= 0) throw new ArgumentException("A saved Inquiry is required.", nameof(inquiry));
            await using var db = new InquiryDbContext();
            var stored = await db.Inquiries.SingleOrDefaultAsync(x => x.Id == inquiry.Id, ct).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Inquiry no longer exists.");
            stored.InquiryReference = inquiry.InquiryReference;
            stored.CustomerId = inquiry.CustomerId; stored.CustomerName = inquiry.CustomerName;
            stored.CustomerLocation = inquiry.CustomerLocation; stored.ProjectId = inquiry.ProjectId;
            stored.ProjectName = inquiry.ProjectName; stored.ProjectDescription = inquiry.ProjectDescription;
            stored.SalesmanId = inquiry.SalesmanId; stored.SalesmanName = inquiry.SalesmanName;
            stored.PreparedByUserId = inquiry.PreparedByUserId; stored.PreparedByName = inquiry.PreparedByName;
            stored.PreparedByRole = inquiry.PreparedByRole; stored.Priority = inquiry.Priority;
            stored.InquiryValue = inquiry.InquiryValue; stored.Notes = inquiry.Notes;
            stored.UpdatedAt = DateTimeOffset.Now; stored.UpdatedBy = inquiry.UpdatedBy;
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            return stored;
        }

        public async Task<bool> ChangeStatusAsync(int id, string status, string actor, CancellationToken ct = default)
        {
            await using var db = new InquiryDbContext();
            var row = await db.Inquiries.SingleOrDefaultAsync(x => x.Id == id, ct).ConfigureAwait(false);
            if (row == null) return false;
            string next = InquiryStatuses.Normalize(status);
            row.Status = next; row.UpdatedAt = DateTimeOffset.Now; row.UpdatedBy = actor ?? "";
            if (next == InquiryStatuses.Prepared) row.PreparedAt ??= row.UpdatedAt;
            if (next == InquiryStatuses.Sent) row.SentAt ??= row.UpdatedAt;
            if (next == InquiryStatuses.Confirmed) row.ConfirmedAt ??= row.UpdatedAt;
            db.Communications.Add(new InquiryCommunication
            {
                InquiryId = id, EventType = next, Title = $"Inquiry {next}",
                Description = $"Status changed to {next}.", OccurredAt = row.UpdatedAt,
                ActorName = actor ?? ""
            });
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            return true;
        }

        public async Task<IReadOnlyList<InquiryAttachment>> GetAttachmentsAsync(int inquiryId, CancellationToken ct = default)
        {
            await using var db = new InquiryDbContext();
            return await db.Attachments.AsNoTracking().Where(x => x.InquiryId == inquiryId)
                .OrderByDescending(x => x.UploadedAt).ToListAsync(ct).ConfigureAwait(false);
        }

        public async Task<InquiryAttachment> AddAttachmentAsync(InquiryAttachment attachment, CancellationToken ct = default)
        {
            await using var db = new InquiryDbContext();
            db.Attachments.Add(attachment);
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            return attachment;
        }

        public async Task<bool> DeleteAttachmentAsync(long attachmentId, CancellationToken ct = default)
        {
            await using var db = new InquiryDbContext();
            var row = await db.Attachments.SingleOrDefaultAsync(x => x.Id == attachmentId, ct).ConfigureAwait(false);
            if (row == null) return false;
            db.Attachments.Remove(row);
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            return true;
        }

        public async Task<IReadOnlyList<InquiryCommunication>> GetCommunicationsAsync(int inquiryId, CancellationToken ct = default)
        {
            await using var db = new InquiryDbContext();
            return await db.Communications.AsNoTracking().Where(x => x.InquiryId == inquiryId)
                .OrderByDescending(x => x.OccurredAt).ToListAsync(ct).ConfigureAwait(false);
        }

        private static IQueryable<InquiryRecord> ApplyFilters(
            IQueryable<InquiryRecord> source, InquiryQuery query, bool includeStatus)
        {
            string search = (query.Search ?? "").Trim();
            if (search.Length > 0)
                source = source.Where(x => x.InquiryNumber.Contains(search)
                    || x.CustomerName.Contains(search) || x.ProjectName.Contains(search)
                    || x.SalesmanName.Contains(search) || x.PreparedByName.Contains(search));
            if (includeStatus && !string.IsNullOrWhiteSpace(query.Status)
                && !query.Status.StartsWith("All", StringComparison.OrdinalIgnoreCase))
                source = source.Where(x => x.Status == query.Status);
            if (query.SalesmanId.HasValue) source = source.Where(x => x.SalesmanId == query.SalesmanId);
            else if (!string.IsNullOrWhiteSpace(query.SalesmanName)
                && !query.SalesmanName.StartsWith("All", StringComparison.OrdinalIgnoreCase))
                source = source.Where(x => x.SalesmanName == query.SalesmanName);
            if (query.PreparedByUserId.HasValue) source = source.Where(x => x.PreparedByUserId == query.PreparedByUserId);
            else if (!string.IsNullOrWhiteSpace(query.PreparedByName)
                && !query.PreparedByName.StartsWith("All", StringComparison.OrdinalIgnoreCase))
                source = source.Where(x => x.PreparedByName == query.PreparedByName);
            if (query.From.HasValue) source = source.Where(x => x.ReceivedAt >= query.From.Value);
            if (query.ToExclusive.HasValue) source = source.Where(x => x.ReceivedAt < query.ToExclusive.Value);
            if (query.HasAttachmentsOnly) source = source.Where(x => x.Attachments.Any());
            if (!string.IsNullOrWhiteSpace(query.Priority)
                && !query.Priority.StartsWith("All", StringComparison.OrdinalIgnoreCase))
                source = source.Where(x => x.Priority == query.Priority);
            return source;
        }
    }
}
