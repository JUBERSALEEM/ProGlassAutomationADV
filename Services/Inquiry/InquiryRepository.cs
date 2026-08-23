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
        private readonly IInquiryLifecycleService _lifecycle;

        public InquiryRepository() : this(new InquiryLifecycleService()) { }

        public InquiryRepository(IInquiryLifecycleService lifecycle)
        {
            _lifecycle = lifecycle ?? throw new ArgumentNullException(nameof(lifecycle));
        }

        public async Task InitializeAsync(CancellationToken ct = default)
        {
            await using var db = new InquiryDbContext();
            await db.Database.EnsureCreatedAsync(ct).ConfigureAwait(false);
            SchemaMigrationRunner.Upgrade(InquiryDbContext.DatabaseFilePath,
                AppSchemaMigrations.Inquiries, "Inquiry");
        }

        public async Task<PagedResult<InquiryListProjection>> QueryAsync(
            InquiryQuery query, CancellationToken ct = default)
        {
            await using var db = new InquiryDbContext();
            var source = ApplyFilters(db.Inquiries.AsNoTracking(), query, includeStatus: true);
            source = query.Sort switch
            {
                "Updated oldest" => source.OrderBy(x => x.UpdatedAt),
                "Received oldest" => source.OrderBy(x => x.ReceivedAt),
                "Received newest" => source.OrderByDescending(x => x.ReceivedAt),
                "Inquiry number" => source.OrderBy(x => x.InquiryNumber),
                "Inquiry number descending" => source.OrderByDescending(x => x.InquiryNumber),
                "Value highest" => source.OrderByDescending(x => x.InquiryValue),
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
                    InquiryValue = x.InquiryValue, Notes = x.Notes,
                    ReceivedAt = x.ReceivedAt, UpdatedAt = x.UpdatedAt, UpdatedBy = x.UpdatedBy,
                    AttachmentCount = x.Attachments.Count, ProformaInvoiceId = x.ProformaInvoiceId,
                    ProformaNumber = x.ProformaNumber, JobOrderNumber = x.JobOrderNumber
                }, query.Page, Math.Clamp(query.PageSize, 10, 200), ct).ConfigureAwait(false);
            var ids = page.Items.Select(x => x.Id).ToList();
            if (ids.Count > 0)
            {
                var lifecycle = await _lifecycle.GetLifecycleAsync(ids, ct).ConfigureAwait(false);
                foreach (var row in page.Items)
                {
                    row.Lifecycle = lifecycle.TryGetValue(row.Id, out var snap)
                        ? snap
                        : InquiryLifecycleSnapshot.Empty(row.Id);
                }
            }
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
                    row.AttachmentBadgeText = string.Join(" ", types)
                        + (extra > 0 ? $" +{extra}" : "");
                }
            }
            return page;
        }

        public async Task<InquiryStatistics> GetStatisticsAsync(InquiryQuery query, CancellationToken ct = default)
        {
            await using var db = new InquiryDbContext();
            var source = ApplyFilters(db.Inquiries.AsNoTracking(), query, includeStatus: false);
            var rows = await source.Select(x => new { x.Status, x.InquiryValue })
                .ToListAsync(ct).ConfigureAwait(false);
            int Count(string status) => rows.Count(x => x.Status == status);
            return new InquiryStatistics
            {
                All = rows.Count,
                Received = Count(InquiryStatuses.Received),
                Prepared = Count(InquiryStatuses.Prepared),
                Sent = Count(InquiryStatuses.Sent),
                Confirmed = Count(InquiryStatuses.Confirmed),
                Hold = Count(InquiryStatuses.Hold),
                Cancelled = Count(InquiryStatuses.Cancelled),
                OpenPipelineValue = rows.Where(x => x.Status != InquiryStatuses.Cancelled)
                    .Sum(x => x.InquiryValue)
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

            // S5 fix: unique-index collisions (concurrent save, import batch) are
            // retried with the next sequence number instead of surfacing a raw
            // DbUpdateException and losing the save.
            for (int attempt = 0; ; attempt++)
            {
                if (string.IsNullOrWhiteSpace(inquiry.InquiryNumber) || attempt > 0)
                    inquiry.InquiryNumber = await NextInquiryNumberAsync(db, ct).ConfigureAwait(false);
                var now = DateTime.Now;
                inquiry.Status = InquiryStatuses.Normalize(inquiry.Status);
                inquiry.CreatedAt = now; inquiry.UpdatedAt = now;
                if (inquiry.ReceivedAt == default) inquiry.ReceivedAt = now;
                db.Inquiries.Add(inquiry);
            try
            {
                await db.SaveChangesAsync(ct).ConfigureAwait(false);
                _lifecycle.Invalidate(inquiry.Id);
                return inquiry;
            }
            catch (DbUpdateException ex) when (attempt < 5 && IsUniqueViolation(ex))
            {
                db.Entry(inquiry).State = EntityState.Detached;
            }
            }
        }

        /// <summary>Maximum existing sequence for the current year prefix, plus one.</summary>
        private static async Task<string> NextInquiryNumberAsync(InquiryDbContext db, CancellationToken ct)
        {
            string prefix = $"INQ-{DateTime.Now:yyyy}-";
            var numbers = await db.Inquiries.AsNoTracking()
                .Where(x => x.InquiryNumber.StartsWith(prefix))
                .Select(x => x.InquiryNumber).ToListAsync(ct).ConfigureAwait(false);
            int highest = numbers.Select(x => ParseSequenceSuffix(x, prefix))
                .DefaultIfEmpty(0).Max();
            return prefix + (highest + 1).ToString("D4");
        }

        /// <summary>Pure helper (unit-tested): "INQ-2026-0007" + prefix → 7, junk → 0.</summary>
        public static int ParseSequenceSuffix(string? number, string prefix)
        {
            if (string.IsNullOrEmpty(number) || number.Length <= prefix.Length)
                return 0;
            return int.TryParse(number.Substring(prefix.Length), out int value) ? value : 0;
        }

        private static bool IsUniqueViolation(DbUpdateException ex) =>
            ex.InnerException?.Message.Contains("UNIQUE constraint failed",
                StringComparison.OrdinalIgnoreCase) == true;

        public async Task<InquiryRecord> UpdateAsync(InquiryRecord inquiry, CancellationToken ct = default)
        {
            if (inquiry == null || inquiry.Id <= 0) throw new ArgumentException("A saved Inquiry is required.", nameof(inquiry));
            await using var db = new InquiryDbContext();
            var stored = await db.Inquiries.SingleOrDefaultAsync(x => x.Id == inquiry.Id, ct).ConfigureAwait(false)
                ?? throw new InvalidOperationException("Inquiry no longer exists. It may have been removed by another user — refresh the register.");
            stored.InquiryReference = inquiry.InquiryReference;
            stored.CustomerId = inquiry.CustomerId; stored.CustomerName = inquiry.CustomerName;
            stored.CustomerLocation = inquiry.CustomerLocation; stored.ProjectId = inquiry.ProjectId;
            stored.ProjectName = inquiry.ProjectName; stored.ProjectDescription = inquiry.ProjectDescription;
            stored.SalesmanId = inquiry.SalesmanId; stored.SalesmanName = inquiry.SalesmanName;
            stored.PreparedByUserId = inquiry.PreparedByUserId; stored.PreparedByName = inquiry.PreparedByName;
            stored.PreparedByRole = inquiry.PreparedByRole; stored.Priority = inquiry.Priority;
            stored.InquiryValue = inquiry.InquiryValue; stored.Notes = inquiry.Notes;
            // S2 fix: ReceivedAt is an editable field — persist it. (Never write
            // default; a stale binding must not reset the date to 0001-01-01.)
            if (inquiry.ReceivedAt != default) stored.ReceivedAt = inquiry.ReceivedAt;
            stored.UpdatedAt = DateTime.Now; stored.UpdatedBy = inquiry.UpdatedBy;
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            _lifecycle.Invalidate(stored.Id);
            return stored;
        }

        public async Task<bool> ChangeStatusAsync(int id, string status, string actor, CancellationToken ct = default)
        {
            await using var db = new InquiryDbContext();
            var row = await db.Inquiries.SingleOrDefaultAsync(x => x.Id == id, ct).ConfigureAwait(false);
            if (row == null) return false;
            string next = InquiryStatuses.Normalize(status);
            row.Status = next; row.UpdatedAt = DateTime.Now; row.UpdatedBy = actor ?? "";
            if (next == InquiryStatuses.Prepared)
            {
                row.PreparedAt ??= row.UpdatedAt;
                // Stamp the preparer the first time the record is prepared —
                // the register's "Prepared By" column was silently empty when
                // records were created from import.
                if (string.IsNullOrWhiteSpace(row.PreparedByName))
                {
                    row.PreparedByName = actor ?? "";
                    row.PreparedByUserId ??= Services.Audit.AuditService.CurrentUser?.Id;
                }
            }
            if (next == InquiryStatuses.Sent) row.SentAt ??= row.UpdatedAt;
            if (next == InquiryStatuses.Confirmed) row.ConfirmedAt ??= row.UpdatedAt;
            db.Communications.Add(new InquiryCommunication
            {
                InquiryId = id, EventType = next, Title = $"Inquiry {next}",
                Description = $"Status changed to {next}.", OccurredAt = row.UpdatedAt,
                ActorName = actor ?? ""
            });
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            _lifecycle.Invalidate(id);
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

        public void InvalidateLifecycle(int inquiryId) => _lifecycle.Invalidate(inquiryId);

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
