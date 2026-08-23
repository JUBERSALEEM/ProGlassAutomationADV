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

        public async Task<IReadOnlyList<InquiryAttachment>> GetAttachmentsAsync(int inquiryId, CancellationToken ct = default)
        {
            await using var db = new InquiryDbContext();
            return await db.Attachments.AsNoTracking().Where(x => x.InquiryId == inquiryId)
                .OrderByDescending(x => x.UploadedAt).ToListAsync(ct).ConfigureAwait(false);
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
            return source;
        }
    }
}
