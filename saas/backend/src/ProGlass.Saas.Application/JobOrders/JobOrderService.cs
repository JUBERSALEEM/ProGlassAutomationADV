using System.Text.Json;
using ProGlass.Saas.Application.Auth;
using ProGlass.Saas.Domain.Entities;
using ProGlass.Saas.Domain.Proforma;

namespace ProGlass.Saas.Application.JobOrders;

// ═══════════════════════════════════════════════════════════════════════
//  Phase 9 service — the WPF JobOrderMapper.BuildFromProformaInvoice +
//  JobOrderRepository (insert retry, optimistic RowVersion, soft-close
//  void) + SavedJobsListViewModel's convert guards and ChangeStatusAsync,
//  ported to the SaaS conventions.
//
//  Behaviour notes (mapping §1.7a):
//  • A voided PI is NEVER convertible (the WPF's hard guard).
//  • An already-converted PI WARNED, not blocked (a revised order / a
//    split delivery is legitimate) — the API answers 409 listing the
//    existing job numbers and converts on an explicit AllowExisting
//    re-post.
//  • A CONFIRMED quote (status Approved) produces a job already
//    RELEASED to the factory floor; anything else stays Draft.
//  • The job's recorded figures are copied at conversion and never
//    change after (the WPF's immutable embedded snapshot rule).
//  • The master Delivery Note the WPF creates alongside arrives with
//    the Phase 10 delivery module (documented deferral).
// ═══════════════════════════════════════════════════════════════════════

/// <summary>The Job Order module (core lifecycle).</summary>
public sealed class JobOrderService(IJobOrderStore store, IAuditWriter audit)
{
    private const int NumberAttempts = 5;
    private const int DefaultPageSize = 50;
    private const int MaxPageSize = 200;

    private static readonly JsonSerializerOptions SnapshotJson = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <summary>GET — the tenant's job orders (search on number/customer/
    /// project, optional status filter, voided excluded unless asked).</summary>
    public async Task<MasterData.PagedResult<JobOrderDto>> ListAsync(
        Guid tenantId, string? search, string? status, bool includeVoid,
        int? page, int? pageSize, CancellationToken ct)
    {
        var (p, size) = NormalizePaging(page, pageSize);
        var result = await store.SearchAsync(
            tenantId, search, status, includeVoid, p, size, ct);
        return new MasterData.PagedResult<JobOrderDto>(
            result.Items.Select(ToDto).ToList(),
            result.Page, result.PageSize, result.TotalCount);
    }

    public async Task<JobOrderResult<JobOrderDto>> GetAsync(
        Guid tenantId, Guid id, CancellationToken ct)
    {
        var job = await store.FindAsync(tenantId, id, ct);
        return job is null
            ? JobOrderResult<JobOrderDto>.Fail(
                JobOrderErrorCode.NotFound, $"Job order {id} was not found.")
            : JobOrderResult<JobOrderDto>.Ok(ToDto(job));
    }

    /// <summary>The job orders created from one proforma invoice.</summary>
    public async Task<JobOrderResult<IReadOnlyList<JobOrderDto>>> GetByProformaAsync(
        Guid tenantId, Guid proformaId, CancellationToken ct)
    {
        var jobs = await store.FindByProformaAsync(tenantId, proformaId, ct);
        return JobOrderResult<IReadOnlyList<JobOrderDto>>.Ok(
            jobs.Select(ToDto).ToList());
    }

    /// <summary>
    /// POST — convert a proforma invoice into a Job Order (WPF
    /// CreateJobOrderAsync + JobOrderMapper.BuildFromProformaInvoice).
    /// </summary>
    public async Task<JobOrderResult<JobOrderDto>> CreateFromProformaAsync(
        Guid tenantId, Guid actorId, CreateJobOrderRequest request,
        string? ip, CancellationToken ct)
    {
        var proforma = await store.FindProformaContextAsync(tenantId, request.ProformaId, ct);
        if (proforma is null)
        {
            return JobOrderResult<JobOrderDto>.Fail(JobOrderErrorCode.NotFound,
                $"Proforma invoice {request.ProformaId} was not found.");
        }

        // A voided quote must never become a Job Order — the hard guard
        // (every entry point, not just the picker that hides them).
        if (proforma.Invoice.IsVoid)
        {
            return JobOrderResult<JobOrderDto>.Fail(JobOrderErrorCode.Conflict,
                $"Proforma invoice {proforma.Invoice.ProformaNo} is void and " +
                "cannot be converted to a Job Order.");
        }

        // Warn, not block — the WPF dialog's Yes/No made explicit.
        if (!request.AllowExisting)
        {
            var existing = await store.FindByProformaAsync(
                tenantId, request.ProformaId, ct);
            if (existing.Count > 0)
            {
                var ids = string.Join(", ", existing.Select(j => j.JobNo));
                return JobOrderResult<JobOrderDto>.Fail(JobOrderErrorCode.Conflict,
                    $"Proforma invoice {proforma.Invoice.ProformaNo} already has " +
                    $"{existing.Count} job order(s): {ids}. A quote can be built " +
                    "twice (a revised order, a split delivery) — re-post with " +
                    "allowExisting to confirm.");
            }
        }

        // Derive JOB-… from the PI number; SuggestNext only when taken
        // (the WPF rule), then the repository's 5-attempt retry loop.
        var jobOrderNo = JobOrderNumbers.DeriveJobOrderNo(proforma.Invoice.ProformaNo);
        if (await store.ExistsNoAsync(tenantId, jobOrderNo, ct))
        {
            jobOrderNo = await store.SuggestNextNoAsync(tenantId, jobOrderNo, ct);
        }

        // A CONFIRMED quote is already released to the factory floor.
        var confirmed = string.Equals(
            proforma.Invoice.Status, ProformaStatuses.Approved,
            StringComparison.OrdinalIgnoreCase);
        var now = DateTime.UtcNow;

        var job = new JobOrder
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            JobNo = jobOrderNo,
            ProformaId = proforma.Invoice.Id,
            CustomerId = proforma.Invoice.ClientId,
            ProjectId = proforma.Invoice.ProjectId,
            SalesmanId = proforma.Invoice.SalesmanId,
            CustomerName = proforma.CustomerName,
            ProjectName = proforma.ProjectName,
            GrandTotalVatInclusive = proforma.Invoice.GrandTotalVatInclusive,
            Status = confirmed
                ? JobOrderStatuses.Released
                : JobOrderStatuses.Draft,
            Priority = JobOrderPriorities.Medium,
            DueDate = null,
            RowVersion = 0,
            SnapshotJson = JsonSerializer.Serialize(new
            {
                proformaNo = proforma.Invoice.ProformaNo,
                proformaStatus = proforma.Invoice.Status,
                issueDate = proforma.Invoice.IssueDate,
                validUntil = proforma.Invoice.ValidUntil,
                customerName = proforma.CustomerName,
                projectName = proforma.ProjectName,
                grandTotalVatInclusive = proforma.Invoice.GrandTotalVatInclusive,
                glassMaterialsSubtotal = proforma.Invoice.GlassMaterialsSubtotal,
                otherChargesSubtotal = proforma.Invoice.OtherChargesSubtotal,
                discountAmount = proforma.Invoice.DiscountAmount,
                vatAmount = proforma.Invoice.VatAmount,
                convertedAt = now
            }, SnapshotJson),
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = actorId,
            UpdatedBy = actorId
        };

        for (var attempt = 0; attempt < NumberAttempts; attempt++)
        {
            if (await store.TryInsertAsync(job, ct))
            {
                await AuditAsync(tenantId, actorId, "joborder.created",
                    "job_orders", job.Id, null, new
                    {
                        jobOrderNo = job.JobNo,
                        from = proforma.Invoice.ProformaNo,
                        status = job.Status
                    }, ip, ct);
                return JobOrderResult<JobOrderDto>.Ok(ToDto(job));
            }

            // Unique violation — bump and retry with a fresh context.
            job.JobNo = await store.SuggestNextNoAsync(tenantId, job.JobNo, ct);
        }

        return JobOrderResult<JobOrderDto>.Fail(JobOrderErrorCode.NumberExhausted,
            $"Could not allocate a free job order number after {NumberAttempts} " +
            $"retries starting from '{JobOrderNumbers.DeriveJobOrderNo(proforma.Invoice.ProformaNo)}'.");
    }

    /// <summary>
    /// PUT — the ProductionDetails fields (WPF detail-view save) with the
    /// optimistic RowVersion check: a concurrent save is never silently
    /// overwritten.
    /// </summary>
    public async Task<JobOrderResult<JobOrderDto>> UpdateAsync(
        Guid tenantId, Guid id, Guid actorId, UpdateJobOrderRequest request,
        string? ip, CancellationToken ct)
    {
        var job = await store.FindAsync(tenantId, id, ct);
        if (job is null)
        {
            return JobOrderResult<JobOrderDto>.Fail(
                JobOrderErrorCode.NotFound, $"Job order {id} was not found.");
        }

        var before = ToDto(job);
        var priority = request.Priority is null ? job.Priority : request.Priority.Trim();
        if (!JobOrderPriorities.All.Contains(priority, StringComparer.OrdinalIgnoreCase))
        {
            return JobOrderResult<JobOrderDto>.Fail(JobOrderErrorCode.ValidationFailed,
                $"Priority must be one of: {string.Join(", ", JobOrderPriorities.All)}.");
        }

        job.Priority = priority;
        job.DueDate = request.DeliveryDate ?? job.DueDate;
        job.SiteDeliveryContact = request.SiteDeliveryContact ?? job.SiteDeliveryContact;
        job.SiteDeliveryPhone = request.SiteDeliveryPhone ?? job.SiteDeliveryPhone;
        job.SiteDeliveryAddress = request.SiteDeliveryAddress ?? job.SiteDeliveryAddress;
        job.AssignedTo = request.AssignedTo ?? job.AssignedTo;
        job.CuttingInstructions = request.CuttingInstructions ?? job.CuttingInstructions;
        job.DrawingReference = request.DrawingReference ?? job.DrawingReference;
        job.InternalNotes = request.InternalNotes ?? job.InternalNotes;
        job.Notes = request.Notes ?? job.Notes;
        job.UpdatedAt = DateTime.UtcNow;
        job.UpdatedBy = actorId;

        if (!await store.TryUpdateAsync(job, request.ExpectedRowVersion, ct))
        {
            return JobOrderResult<JobOrderDto>.Fail(JobOrderErrorCode.ConcurrencyConflict,
                $"Job order {job.JobNo} was modified by someone else — reload and " +
                "retry (RowVersion mismatch).");
        }

        await AuditAsync(tenantId, actorId, "joborder.updated",
            "job_orders", job.Id, before, ToDto(job), ip, ct);
        return JobOrderResult<JobOrderDto>.Ok(ToDto(job));
    }

    /// <summary>
    /// POST /{id}/transition — WPF ChangeStatusAsync: the centralized
    /// StatusTransitions table decides (self-transitions allowed; a void
    /// job only recovers through Rejected → Draft).
    /// </summary>
    public async Task<JobOrderResult<JobOrderDto>> TransitionAsync(
        Guid tenantId, Guid id, Guid actorId, JobOrderTransitionRequest request,
        string? ip, CancellationToken ct)
    {
        var job = await store.FindAsync(tenantId, id, ct);
        if (job is null)
        {
            return JobOrderResult<JobOrderDto>.Fail(
                JobOrderErrorCode.NotFound, $"Job order {id} was not found.");
        }

        var next = (request.ToStatus ?? "").Trim();
        if (!JobOrderWorkflow.CanTransition(job.Status, next))
        {
            return JobOrderResult<JobOrderDto>.Fail(JobOrderErrorCode.Conflict,
                JobOrderWorkflow.Reason(job.Status, next));
        }

        var before = job.Status;
        job.Status = next;
        job.UpdatedAt = DateTime.UtcNow;
        job.UpdatedBy = actorId;

        if (!await store.TryUpdateAsync(job, null, ct))
        {
            return JobOrderResult<JobOrderDto>.Fail(JobOrderErrorCode.ConcurrencyConflict,
                $"Job order {job.JobNo} was modified by someone else — reload and retry.");
        }

        await AuditAsync(tenantId, actorId, "joborder.status_changed",
            "job_orders", job.Id, null, new
            {
                from = before,
                to = next,
                reason = request.Reason
            }, ip, ct);
        return JobOrderResult<JobOrderDto>.Ok(ToDto(job));
    }

    /// <summary>
    /// POST /{id}/void — the ERP soft-close: the row survives (number +
    /// conversion snapshot + audit trail), Status moves to the void
    /// target (Rejected) and the void block is stamped. Idempotent.
    /// </summary>
    public async Task<JobOrderResult<JobOrderDto>> VoidAsync(
        Guid tenantId, Guid id, Guid actorId, VoidJobOrderRequest request,
        string? ip, CancellationToken ct)
    {
        var job = await store.FindAsync(tenantId, id, ct);
        if (job is null)
        {
            return JobOrderResult<JobOrderDto>.Fail(
                JobOrderErrorCode.NotFound, $"Job order {id} was not found.");
        }

        if (!job.IsVoid)
        {
            job.IsVoid = true;
            job.VoidedAt = DateTime.UtcNow;
            job.VoidedBy = actorId;
            job.VoidReason = string.IsNullOrWhiteSpace(request.Reason)
                ? "(no reason given)"
                : request.Reason!.Trim();
            job.Status = JobOrderStatuses.Rejected;
            job.UpdatedAt = DateTime.UtcNow;
            job.UpdatedBy = actorId;

            if (!await store.TryUpdateAsync(job, null, ct))
            {
                return JobOrderResult<JobOrderDto>.Fail(
                    JobOrderErrorCode.ConcurrencyConflict,
                    $"Job order {job.JobNo} was modified by someone else — reload and retry.");
            }

            await AuditAsync(tenantId, actorId, "joborder.voided",
                "job_orders", job.Id, null, new
                {
                    jobOrderNo = job.JobNo,
                    reason = job.VoidReason
                }, ip, ct);
        }

        return JobOrderResult<JobOrderDto>.Ok(ToDto(job));
    }

    // ── helpers ─────────────────────────────────────────────────────

    private static (int Page, int Size) NormalizePaging(int? page, int? pageSize)
    {
        var size = Math.Clamp(pageSize ?? DefaultPageSize, 1, MaxPageSize);
        return (Math.Max(page ?? 1, 1), size);
    }

    internal static JobOrderDto ToDto(JobOrder j) => new(
        j.Id, j.JobNo, j.ProformaId, SourceProformaNo(j), j.CustomerName,
        j.ProjectName, j.GrandTotalVatInclusive,
        j.Status, JobOrderStatuses.Canonical(j.Status), j.Priority, j.DueDate,
        j.SiteDeliveryContact, j.SiteDeliveryPhone, j.SiteDeliveryAddress,
        j.AssignedTo, j.CuttingInstructions, j.DrawingReference,
        j.InternalNotes, j.Notes,
        j.IsVoid, j.VoidedAt, j.VoidReason, j.RowVersion,
        j.CreatedAt, j.UpdatedAt);

    private static string SourceProformaNo(JobOrder j) =>
        j.SourceProforma?.ProformaNo ?? "";

    private async Task AuditAsync(
        Guid tenantId, Guid? actorUserId, string action, string entityType,
        Guid entityId, object? before, object? after, string? ip, CancellationToken ct)
        => await audit.WriteAsync(tenantId, actorUserId, action, entityType,
            entityId, ip, null,
            before is null ? null : JsonSerializer.Serialize(before),
            after is null ? null : JsonSerializer.Serialize(after),
            ct).ConfigureAwait(false);
}
