using System.Text.Json;
using ProGlass.Saas.Application.Auth;
using ProGlass.Saas.Domain.Entities;
using ProGlass.Saas.Domain.Pricing;

namespace ProGlass.Saas.Application.Specifications;

/// <summary>
/// The specifications registry — the Step 4 APPLY library and the
/// ledger-derived usage trail.
///
/// Source of truth (read 2026-09-27):
///   • Library writes — SpecificationsRepository.SaveOrUpdateAsync: dedup
///     by GlassType + SpecName, rate always refreshed, catalogue details
///     refreshed only when supplied (preserve-on-blank), UsageCount starts
///     at 0 and is NEVER touched by APPLY. Saving demands ProformaEdit
///     ("a saved spec sets the rate Step 5 and the P&L price against").
///   • Usage — SpecificationLedgerService: counts, history and provenance
///     are DERIVED from SAVED invoices' Step 5 groups only; an APPLY press
///     is not a use until its invoice is saved. PricePerSqm stays
///     estimator-owned. In the SaaS the derivation is set-based over the
///     normalized spec_groups rows (no JSON snapshot parsing needed) and
///     runs after every grid save and on demand — never inside the save's
///     transaction, and a ledger failure must never fail a save (WPF:
///     "NEVER THROWS").
///   • Lists — GetAllAsync smart sort (UsageCount DESC, LastUsedAt DESC);
///     usage history UsedAt DESC; recent usage capped at 200 by default.
///   • Delete — WPF DeleteAsync demands ProformaDelete.
///
/// Differences from WPF (documented in WPF_TO_SAAS_MAPPING §1.3a):
///   • DELETE answers 404 for a missing id (WPF silently no-ops).
///   • Usage rows are one per (spec, invoice) — the WPF's per-save
///     appends were exactly what its own ledger had to purge.
///   • The startup/debounced reconcile becomes: reconcile after every
///     Step 5 grid save (tolerant) + POST /specifications/reconcile.
/// </summary>
public sealed class SpecificationsService
{
    /// <summary>WPF GetRecentUsageAsync default.</summary>
    public const int DefaultRecentLimit = 200;

    private readonly ISpecificationsStore _store;
    private readonly IAuditWriter _audit;

    public SpecificationsService(ISpecificationsStore store, IAuditWriter audit)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
    }

    // ═════════════════════════════ APPLY (Step 4) ══════════════════

    public async Task<SpecificationResult<SpecificationDto>> ApplyAsync(
        Guid tenantId, Guid actorId, string? ip, ApplySpecificationRequest? request,
        CancellationToken ct)
    {
        if (request is null)
        {
            return SpecificationResult<SpecificationDto>.Fail(
                SpecificationErrorCode.ValidationFailed, "Request body is required.");
        }

        var glassType = (request.GlassType ?? "").Trim();
        var specName = (request.SpecName ?? "").Trim();

        if (!GlassTypes.All.Contains(glassType))
        {
            return SpecificationResult<SpecificationDto>.Fail(
                SpecificationErrorCode.ValidationFailed,
                $"Unknown glass type '{request.GlassType}'. " +
                $"Valid: {string.Join(", ", GlassTypes.All)}.");
        }

        if (specName.Length == 0)
        {
            return SpecificationResult<SpecificationDto>.Fail(
                SpecificationErrorCode.ValidationFailed, "Spec name is required.");
        }

        if (request.RatePerSqm < 0)
        {
            return SpecificationResult<SpecificationDto>.Fail(
                SpecificationErrorCode.ValidationFailed,
                "Rate per SQM cannot be negative.");
        }

        var existing = await _store.FindByKeyAsync(tenantId, glassType, specName, ct);

        if (existing is not null)
        {
            // LEDGER GROUND TRUTH: this writes the reusable LIBRARY so
            // Step 5 can offer the spec before its invoice is saved. It
            // deliberately does NOT touch UsageCount / LastUsedAt.
            existing.RatePerSqm = request.RatePerSqm;
            if (request.CostBasisPerSqm is not null)
            {
                existing.CostBasisPerSqm = request.CostBasisPerSqm.Value;
            }

            if (!string.IsNullOrWhiteSpace(request.ParamsJson))
            {
                existing.ParamsJson = request.ParamsJson;
            }

            // Refresh catalogue columns, but only when the caller actually
            // supplied a value — a valueless APPLY must not wipe details an
            // earlier, better-informed save already recorded.
            if (!string.IsNullOrWhiteSpace(request.Category)) existing.Category = request.Category;
            if (!string.IsNullOrWhiteSpace(request.Color)) existing.Color = request.Color;
            if (request.ThicknessMm is > 0) existing.ThicknessMm = request.ThicknessMm.Value;
            if (request.SheetWidthMm is > 0 && request.SheetHeightMm is > 0)
            {
                existing.SheetWidthMm = request.SheetWidthMm.Value;
                existing.SheetHeightMm = request.SheetHeightMm.Value;
            }

            if (request.SheetId is not null) existing.SheetId = request.SheetId;
            if (!string.IsNullOrWhiteSpace(request.PanesJson)) existing.PanesJson = request.PanesJson;

            await _store.UpdateAsync(existing, actorId, ct);

            await _audit.WriteAsync(
                tenantId, actorId, "specifications.applied", "specifications",
                existing.Id, ip, null, ct,
                afterJson: JsonSerializer.Serialize(new
                {
                    existing.SpecName,
                    existing.GlassType,
                    existing.RatePerSqm,
                    updated = true,
                }));

            return SpecificationResult<SpecificationDto>.Ok(ToDto(existing));
        }

        // Create new spec. UsageCount starts at ZERO — being built in
        // Step 4 is not being used; the ledger sets the verified count
        // from saved invoices. LastUsedAt starts at creation (a touch
        // timestamp, healed by the ledger on the next reconcile).
        var spec = new Specification
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            GlassType = glassType,
            SpecName = specName,
            RatePerSqm = request.RatePerSqm,
            CostBasisPerSqm = request.CostBasisPerSqm ?? 0m,
            ParamsJson = request.ParamsJson,
            Category = request.Category,
            Color = request.Color,
            ThicknessMm = request.ThicknessMm ?? 0,
            SheetWidthMm = request.SheetWidthMm ?? 0,
            SheetHeightMm = request.SheetHeightMm ?? 0,
            SheetId = request.SheetId,
            PanesJson = request.PanesJson,
            UsageCount = 0,
            LastUsedAt = DateTime.UtcNow,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = actorId,
            UpdatedBy = actorId,
        };

        await _store.InsertAsync(spec, ct);

        await _audit.WriteAsync(
            tenantId, actorId, "specifications.applied", "specifications",
            spec.Id, ip, null, ct,
            afterJson: JsonSerializer.Serialize(new
            {
                spec.SpecName,
                spec.GlassType,
                spec.RatePerSqm,
                updated = false,
            }));

        return SpecificationResult<SpecificationDto>.Ok(ToDto(spec));
    }

    // ═════════════════════════════ READS ══════════════════════════

    public async Task<SpecificationResult<SpecificationDto>> GetAsync(
        Guid tenantId, Guid id, CancellationToken ct)
    {
        var spec = await _store.FindByIdAsync(tenantId, id, ct);
        return spec is null
            ? SpecificationResult<SpecificationDto>.Fail(
                SpecificationErrorCode.NotFound,
                $"Specification {id} was not found.")
            : SpecificationResult<SpecificationDto>.Ok(ToDto(spec));
    }

    /// <summary>WPF GetAllAsync: optional glass-type filter, smart sort
    /// (most-used first, then most recent).</summary>
    public async Task<IReadOnlyList<SpecificationDto>> ListAsync(
        Guid tenantId, string? glassType, CancellationToken ct)
    {
        var filter = string.IsNullOrWhiteSpace(glassType) ? null : glassType.Trim();
        var specs = await _store.ListAsync(tenantId, filter, ct);
        return specs.Select(ToDto).ToList();
    }

    public async Task<SpecificationResult<IReadOnlyList<SpecificationUsageDto>>> GetUsageHistoryAsync(
        Guid tenantId, Guid specId, CancellationToken ct)
    {
        var spec = await _store.FindByIdAsync(tenantId, specId, ct);
        if (spec is null)
        {
            return SpecificationResult<IReadOnlyList<SpecificationUsageDto>>.Fail(
                SpecificationErrorCode.NotFound,
                $"Specification {specId} was not found.");
        }

        var rows = await _store.GetUsageHistoryAsync(tenantId, specId, ct);
        return SpecificationResult<IReadOnlyList<SpecificationUsageDto>>.Ok(
            rows.Select(ToDto).ToList());
    }

    public async Task<IReadOnlyList<SpecificationUsageDto>> GetRecentUsageAsync(
        Guid tenantId, int? limit, CancellationToken ct)
    {
        // WPF: if (limit <= 0) limit = 200.
        var effective = limit is <= 0 ? DefaultRecentLimit : limit.Value;
        var rows = await _store.GetRecentUsageAsync(tenantId, effective, ct);
        return rows.Select(ToDto).ToList();
    }

    // ═════════════════════════════ DELETE ═════════════════════════

    public async Task<SpecificationResult<object>> DeleteAsync(
        Guid tenantId, Guid id, Guid actorId, string? ip, CancellationToken ct)
    {
        var spec = await _store.FindByIdAsync(tenantId, id, ct);
        if (spec is null)
        {
            // WPF silently no-ops on a missing id; the API answers 404
            // (documented difference, same as master data).
            return SpecificationResult<object>.Fail(
                SpecificationErrorCode.NotFound,
                $"Specification {id} was not found.");
        }

        await _store.DeleteAsync(tenantId, id, ct);

        await _audit.WriteAsync(
            tenantId, actorId, "specifications.deleted", "specifications",
            id, ip, null, ct,
            beforeJson: JsonSerializer.Serialize(new { spec.SpecName, spec.GlassType }));

        return SpecificationResult<object>.Ok(new { deleted = true });
    }

    // ═════════════════════════════ LEDGER ═════════════════════════

    /// <summary>
    /// Derive counts, history and provenance from SAVED invoices' Step 5
    /// groups (the WPF ledger, set-based). Called after every grid save
    /// (tolerantly — a ledger failure must never fail a save) and from
    /// POST /specifications/reconcile (the Center's Refresh).
    /// </summary>
    public async Task<ReconcileResult> ReconcileAsync(
        Guid tenantId, Guid? actorId, string? ip, CancellationToken ct)
    {
        var (usageRows, purged, healed) = await _store.ReconcileUsageAsync(tenantId, ct);

        await _audit.WriteAsync(
            tenantId, actorId, "specifications.reconciled", "specifications",
            null, ip, null, ct,
            afterJson: JsonSerializer.Serialize(new { usageRows, purged, healed }));

        return new ReconcileResult(usageRows, purged, healed);
    }

    // ═════════════════════════════ MAPPING ════════════════════════

    private static SpecificationDto ToDto(Specification s) => new(
        s.Id, s.SpecName, s.GlassType,
        s.RatePerSqm, s.CostBasisPerSqm,
        s.UsageCount, s.LastUsedAt, s.IsActive,
        s.Category, s.Color, s.ThicknessMm,
        s.SheetWidthMm, s.SheetHeightMm, s.SheetId,
        s.PanesJson, s.LastProformaNo,
        s.CreatedAt, s.UpdatedAt);

    /// <summary>Provenance note: the WPF stored the PI NUMBER string on
    /// the usage row; the SaaS stores the invoice id and the store loads
    /// the Proforma nav, so the number is always the saved invoice's own.
    /// </summary>
    private static SpecificationUsageDto ToDto(SpecificationUsage u) => new(
        u.Id, u.SpecId, u.ProformaId,
        u.Proforma?.ProformaNo ?? "",
        u.SpecName, u.GlassType,
        u.CompanyName, u.CustomerName, u.ProjectName,
        u.PricePerSqm, u.SheetWidthMm, u.SheetHeightMm,
        u.UsedAt);
}
