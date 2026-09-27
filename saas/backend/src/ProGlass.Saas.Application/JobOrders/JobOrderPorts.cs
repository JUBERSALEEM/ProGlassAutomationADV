using ProGlass.Saas.Domain.Entities;

namespace ProGlass.Saas.Application.JobOrders;

/// <summary>
/// Storage port for the Job Order module (Phase 9). The insert follows
/// the WPF's bump-and-retry number allocation (exists-check + unique-
/// violation retry, 5 attempts, fresh context per attempt).
/// </summary>
/// <summary>The source proforma + the resolved party names for a
/// conversion (the WPF read Client.CustomerName / the project resolution
/// out of the embedded snapshot; the SaaS resolves them from the
/// normalized links).</summary>
public sealed record ProformaContext(
    ProformaInvoice Invoice,
    string CustomerName,
    string ProjectName);

public interface IJobOrderStore
{
    Task<ProformaContext?> FindProformaContextAsync(
        Guid tenantId, Guid proformaId, CancellationToken ct);

    Task<MasterData.PagedResult<JobOrder>> SearchAsync(
        Guid tenantId, string? search, string? status, bool includeVoid,
        int page, int pageSize, CancellationToken ct);

    Task<JobOrder?> FindAsync(Guid tenantId, Guid id, CancellationToken ct);

    Task<IReadOnlyList<JobOrder>> FindByProformaAsync(
        Guid tenantId, Guid proformaId, CancellationToken ct);

    Task<bool> ExistsNoAsync(Guid tenantId, string jobOrderNo, CancellationToken ct);

    /// <summary>The next free number at or after the suggestion (the WPF
    /// while-loop that increments until unused).</summary>
    Task<string> SuggestNextNoAsync(
        Guid tenantId, string baseNo, CancellationToken ct);

    /// <summary>One-transaction insert; false on the UNIQUE (tenant, job_no)
    /// violation.</summary>
    Task<bool> TryInsertAsync(JobOrder jobOrder, CancellationToken ct);

    /// <summary>Tracked update with the WPF's optimistic RowVersion check:
    /// expectedRowVersion mismatch ⇒ false (concurrency conflict).</summary>
    Task<bool> TryUpdateAsync(
        JobOrder jobOrder, int? expectedRowVersion, CancellationToken ct);
}
