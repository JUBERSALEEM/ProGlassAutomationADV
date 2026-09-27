// ═══════════════════════════════════════════════════════════════════════
//  FILE: ProGlass.Saas.Domain/Entities/CuttingCosts.cs
//  Phase 26b — allocation ledger, actual costs, replacements and the
//  cutting session family, mapped to the 0022 tables. WPF source of
//  truth: SavedProductionPhase14Entities.cs (SavedMaterialAllocation,
//  SavedActualCost, SavedProductionReplacement) and
//  SavedProductionPhase15Entities.cs (SavedCuttingSession,
//  SavedCuttingSheet, SavedCuttingPlacedPiece).
// ═══════════════════════════════════════════════════════════════════════

using ProGlass.Saas.Domain.Tenancy;

namespace ProGlass.Saas.Domain.Entities
{
    /// <summary>Maps to saas.material_allocations.</summary>
    public sealed class MaterialAllocation : ITenantOwned
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public string AllocationId { get; set; } = null!;
        public Guid? ReservationId { get; set; }
        public string ReservationKey { get; set; } = null!;
        public Guid? JobOrderId { get; set; }
        public string JobRef { get; set; } = null!;
        public int SpecGroupNumber { get; set; }
        public int SerialNumber { get; set; }
        public Guid? OptimizationSessionId { get; set; }
        public int SheetNo { get; set; }
        public string PlacedPieceLabel { get; set; } = null!;
        public string GlassRef { get; set; } = null!;
        public int WidthMm { get; set; }
        public int HeightMm { get; set; }
        public int Quantity { get; set; }
        public decimal UtilizationPct { get; set; }
        public decimal WastagePct { get; set; }
        public string Status { get; set; } = null!;
        public Guid? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ConsumedAt { get; set; }
        public string Notes { get; set; } = null!;
        public JobOrder? JobOrder { get; set; }
        public MaterialReservation? Reservation { get; set; }
    }

    /// <summary>Maps to saas.production_actual_costs.</summary>
    public sealed class ActualCost : ITenantOwned
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public string CostId { get; set; } = null!;
        public Guid? JobOrderId { get; set; }
        public string JobRef { get; set; } = null!;
        public string SourceProformaId { get; set; } = null!;
        public int SpecGroupNumber { get; set; }
        public int SerialNumber { get; set; }
        public string GlassRef { get; set; } = null!;
        public string SpecName { get; set; } = null!;
        public string CostType { get; set; } = null!;
        public Guid? StockSheetId { get; set; }
        public string MaterialAllocationId { get; set; } = null!;
        public decimal Quantity { get; set; }
        public decimal UnitCost { get; set; }
        public decimal TotalCost { get; set; }
        public decimal SellingValue { get; set; }
        public bool IsFoc { get; set; }
        public Guid? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Notes { get; set; } = null!;
        public JobOrder? JobOrder { get; set; }
    }

    /// <summary>Maps to saas.production_replacements.</summary>
    public sealed class ProductionReplacement : ITenantOwned
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public string ReplacementId { get; set; } = null!;
        public Guid? JobOrderId { get; set; }
        public string JobRef { get; set; } = null!;
        public string SourceProformaId { get; set; } = null!;
        public int SpecGroupNumber { get; set; }
        public int SerialNumber { get; set; }
        public string GlassRef { get; set; } = null!;
        public string SpecName { get; set; } = null!;
        public string Position { get; set; } = null!;
        public string Stage { get; set; } = null!;
        public Guid? OriginalPieceId { get; set; }
        public Guid? OriginalEventId { get; set; }
        public Guid? ReplacementPieceId { get; set; }
        public string EntryType { get; set; } = null!;
        public string Reason { get; set; } = null!;
        public int Quantity { get; set; }
        public bool IsFoc { get; set; }
        public int ReplacementSequence { get; set; }
        public Guid? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Status { get; set; } = null!;
        public JobOrder? JobOrder { get; set; }
        public ProductionPiece? OriginalPiece { get; set; }
        public ProductionPiece? ReplacementPiece { get; set; }
    }

    /// <summary>Maps to saas.cutting_sessions.</summary>
    public sealed class CuttingSession : ITenantOwned
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public string CuttingSessionId { get; set; } = null!;
        public Guid? JobOrderId { get; set; }
        public string JobRef { get; set; } = null!;
        public string SourceProformaId { get; set; } = null!;
        public Guid? OptimizationSessionId { get; set; }
        public string MaterialGroupId { get; set; } = null!;
        public int TotalPieces { get; set; }
        public int SheetsUsed { get; set; }
        public decimal UtilizationPct { get; set; }
        public decimal WastagePct { get; set; }
        public string Status { get; set; } = null!;
        public string RotationPolicy { get; set; } = null!;
        public string SplitMode { get; set; } = null!;
        public int EffortLevel { get; set; }
        public string MarginProfileJson { get; set; } = null!;
        public bool IsVerified { get; set; }
        public string VerificationResult { get; set; } = null!;
        public Guid? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string Notes { get; set; } = null!;
        public JobOrder? JobOrder { get; set; }
    }

    /// <summary>Maps to saas.cutting_sheets.</summary>
    public sealed class CuttingSheet : ITenantOwned
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public string CuttingSheetId { get; set; } = null!;
        public Guid CuttingSessionId { get; set; }
        public Guid? JobOrderId { get; set; }
        public string JobRef { get; set; } = null!;
        public int SheetNo { get; set; }
        public Guid? StockSheetId { get; set; }
        public string StockSizeLabel { get; set; } = null!;
        public int StockWidthMm { get; set; }
        public int StockHeightMm { get; set; }
        public int ThicknessMm { get; set; }
        public string Color { get; set; } = null!;
        public int UsableX { get; set; }
        public int UsableY { get; set; }
        public int UsableWidthMm { get; set; }
        public int UsableHeightMm { get; set; }
        public decimal UsedSqm { get; set; }
        public decimal StockSqm { get; set; }
        public decimal UtilizationPct { get; set; }
        public decimal WastagePct { get; set; }
        public bool IsVerified { get; set; }
        public string VerificationResult { get; set; } = null!;
        public int PlacedPieceCount { get; set; }
        public Guid? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public CuttingSession Session { get; set; } = null!;
    }

    /// <summary>Maps to saas.cutting_placed_pieces.</summary>
    public sealed class CuttingPlacedPiece : ITenantOwned
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public string PlacedPieceId { get; set; } = null!;
        public Guid CuttingSheetId { get; set; }
        public Guid CuttingSessionId { get; set; }
        public Guid? JobOrderId { get; set; }
        public string JobRef { get; set; } = null!;
        public int SpecGroupNumber { get; set; }
        public int SerialNumber { get; set; }
        public string GlassRef { get; set; } = null!;
        public string SpecName { get; set; } = null!;
        public string Position { get; set; } = null!;
        public string PieceId { get; set; } = null!;
        public string PieceLabel { get; set; } = null!;
        public int WidthMm { get; set; }
        public int HeightMm { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public bool IsRotated { get; set; }
        public int CutSequence { get; set; }
        public string Status { get; set; } = null!;
        public Guid? SourcePieceId { get; set; }
        public string ReservationKey { get; set; } = null!;
        public string AllocationId { get; set; } = null!;
        public bool IsFoc { get; set; }
        public Guid? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Notes { get; set; } = null!;
        public CuttingSheet Sheet { get; set; } = null!;
    }
}
