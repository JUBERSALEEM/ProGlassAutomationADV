using ProGlass.Saas.Domain.Proforma;

namespace ProGlass.Saas.Application.Proforma;

/// <summary>Proforma failure codes (clients branch on the code, per the
/// API §0 error contract).</summary>
public enum ProformaErrorCode
{
    None = 0,
    ValidationFailed,
    NotFound,
    /// <summary>Invalid lifecycle transition / non-editable body /
    /// terminal document — the WPF no-ops or dialogs; the API answers 409.</summary>
    Conflict,
    /// <summary>Could not allocate a free ProformaNo after 5 retries.</summary>
    NumberExhausted,
}

/// <summary>Typed result: a value or a code + operator-readable message.</summary>
public sealed record ProformaResult<T>(
    bool Success, T? Value, ProformaErrorCode ErrorCode, string Message)
{
    public static ProformaResult<T> Ok(T value) =>
        new(true, value, ProformaErrorCode.None, "");

    public static ProformaResult<T> Fail(ProformaErrorCode code, string message) =>
        new(false, default, code, message);
}

/// <summary>A Step 5 group with its row grid — the store's unit for the
/// whole-grid replace (the generated EF model has no collection nav from
/// SpecGroup to SpecGroupItem, so the pair travels explicitly).</summary>
public sealed record SpecGroupWithItems(
    Domain.Entities.SpecGroup Group,
    IReadOnlyList<Domain.Entities.SpecGroupItem> Items);

// ── Requests ───────────────────────────────────────────────────────────

/// <summary>
/// POST /api/v1/proforma-invoices — creates a Draft. The Step 1 header
/// cards: company/client/project/salesman references, dates, and the
/// status picker (WPF StatusOptions = ProformaStatuses.All).
/// </summary>
public sealed record CreateProformaRequest(
    Guid? CompanyId,
    Guid? ClientId,
    Guid? ProjectId,
    Guid? SalesmanId,
    Guid? InquiryId,
    DateOnly? IssueDate,
    DateOnly? ValidUntil,
    string? Status);

/// <summary>POST /{id}/transitions — machine target state, WPF names
/// ("PendingApproval", "Approved", …; legacy "Pending"/"Active" parse).</summary>
public sealed record TransitionRequest(string ToState, string? Reason);

/// <summary>POST /{id}/void — soft-close. Blank reason is stored as
/// "(no reason given)" (WPF verbatim).</summary>
public sealed record VoidRequest(string? Reason);

/// <summary>
/// One Step 5 grid row (GlassItemRow): two measurements (W1/H1 primary,
/// W2/H2 secondary — DGU panes differ), quantity, perimeter factor.
/// A row with all-zero dimensions is a PHANTOM — allowed, dropped before
/// saving (WPF phantom-row rule).
/// </summary>
public sealed record SpecGroupItemRequest(
    int RowSerial,
    string Position,
    int W1, int H1,
    int W2, int H2,
    int Qty,
    int? PerimeterFactor,
    string Notes);

/// <summary>One Step 5 spec group: the priced spec + its row grid.
/// SpecId links the group to the specifications registry (the ledger
/// derives verified usage from this reference).</summary>
public sealed record SpecGroupRequest(
    int GroupNumber,
    string GlassType,
    string SpecName,
    decimal RatePerSqm,
    string PriceSource,
    IReadOnlyList<SpecGroupItemRequest> Items,
    Guid? SpecId = null,
    // Step 4 provenance (SpecGroupSnapshot Phase G): pane colours + the
    // stock sheets the group was QUOTED on, so Step 6 wastage sources and
    // Step 9 re-costing read the invoice's own inputs, not live prices.
    string? OuterGlassColor = null,
    string? InnerGlassColor = null,
    string? ThirdGlassColor = null,
    int? OuterSheetWidthMm = null,
    int? OuterSheetHeightMm = null,
    int? InnerSheetWidthMm = null,
    int? InnerSheetHeightMm = null,
    int? ThirdSheetWidthMm = null,
    int? ThirdSheetHeightMm = null,
    decimal? OuterSheetPricePerSqm = null,
    decimal? InnerSheetPricePerSqm = null,
    decimal? ThirdSheetPricePerSqm = null,
    // Step 4 production-cost provenance (0010): the rates THIS group was
    // quoted with — Step 9 re-costs the job against them, never against a
    // live Step 4 tab. Null = legacy group, the P&L falls back.
    decimal? Step4SpacerPricePerSqm = null,
    decimal? Step4InterlayerCostPerSqm = null,
    decimal? Step4CuttingCostPerSqm = null,
    decimal? Step4TemperingCostPerSqm = null,
    decimal? UInsertPricePerSqm = null);

/// <summary>PUT /{id}/spec-groups — replaces the whole Step 5 grid for the
/// invoice (wizard save semantics: the grid is the unit of work).</summary>
public sealed record SaveSpecGroupsRequest(IReadOnlyList<SpecGroupRequest> Groups);

/// <summary>
/// POST /api/v1/inquiries/{id}/convert-to-proforma — the created draft
/// plus what the BOQ import did (partial rows deliberately stay on the
/// inquiry as notes).
/// </summary>
public sealed record ConvertInquiryResultDto(
    ProformaDto Invoice,
    int ImportedSpecGroups,
    int SkippedBoqLines);

// ── Read models ────────────────────────────────────────────────────────

/// <summary>A saved proforma invoice row, plus the machine's view of it
/// (valid next transitions, editable flag).</summary>
public sealed record ProformaDto(
    Guid Id, string ProformaNo,
    Guid? RootProformaId, int RevisionNumber,
    Guid? InquiryId, Guid? CompanyId, Guid? ClientId, Guid? ProjectId,
    Guid? SalesmanId,
    string Status, string LifecycleState,
    DateOnly? IssueDate, DateOnly? ValidUntil,
    decimal GlassMaterialsSubtotal, decimal OtherChargesSubtotal,
    decimal DiscountAmount, decimal VatAmount, decimal GrandTotalVatInclusive,
    bool IsVoid, DateTime? VoidedAt, Guid? VoidedBy, string VoidReason,
    int FurthestStep, int RowVersion,
    DateTime? SentAt, DateTime? ApprovedAt, DateTime? ConfirmedAt,
    DateTime CreatedAt, DateTime UpdatedAt,
    bool IsEditable, IReadOnlyList<string> ValidTransitions);

/// <summary>A Step 5 row with its computed values (sqm/lm are derived from
/// the stored dimensions on every read — the WPF grid recalculates the
/// same way; sqm/total_sqm/unit_price/total are also stored).</summary>
public sealed record SpecGroupItemDto(
    Guid Id, int RowSerial, string Position,
    int W1, int H1, int W2, int H2, int Qty,
    decimal Sqm, decimal TotalSqm,
    decimal Sqm2, decimal TotalSqm2,
    decimal Lm1, decimal Lm2,
    decimal UnitPrice, decimal Total,
    string Notes);

public sealed record SpecGroupDto(
    Guid Id, int GroupNumber, string GlassType, string SpecName,
    decimal RatePerSqm, string PriceSource, Guid? SpecId,
    string OuterGlassColor, string InnerGlassColor, string ThirdGlassColor,
    int OuterSheetWidthMm, int OuterSheetHeightMm,
    int InnerSheetWidthMm, int InnerSheetHeightMm,
    int ThirdSheetWidthMm, int ThirdSheetHeightMm,
    decimal OuterSheetPricePerSqm, decimal InnerSheetPricePerSqm,
    decimal ThirdSheetPricePerSqm,
    int TotalQty, decimal TotalSqm,
    IReadOnlyList<SpecGroupItemDto> Items);
