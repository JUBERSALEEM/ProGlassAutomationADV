// ═══════════════════════════════════════════════════════════════════════
// GENERATED FILE — DO NOT EDIT BY HAND
// Source of truth: saas/database/migrations/*.sql (verified by
// saas/database/verify/apply_and_test.py against live PostgreSQL).
// Regenerate with: python3 saas/devbox/generate_ef_model.py
// ═══════════════════════════════════════════════════════════════════════

using ProGlass.Saas.Domain.Tenancy;

namespace ProGlass.Saas.Domain.Entities
{
    /// <summary>Maps to saas.optimization_sessions.</summary>
    public sealed class OptimizationSession : ITenantOwned
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public string DocumentType { get; set; } = null!;
        public Guid? DocumentId { get; set; }
        public string CompositionKey { get; set; } = null!;
        public string? ParamsJson { get; set; }
        public string Status { get; set; } = null!;
        public string Strategy { get; set; } = null!;
        public int SheetsUsed { get; set; }
        public int UnplacedCount { get; set; }
        public decimal TotalUsedSqm { get; set; }
        public decimal TotalStockSqm { get; set; }
        public decimal UtilizationPct { get; set; }
        public bool ConservationVerified { get; set; }
        public bool GeometryVerified { get; set; }
        public string ValidationProblems { get; set; } = null!;
        public string ScaleNotes { get; set; } = null!;
        public int SheetsLowerBound { get; set; }
        public Guid? CreatedBy { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public Guid? UpdatedBy { get; set; }
    }
}

namespace ProGlass.Saas.Domain.Entities
{
    /// <summary>Maps to saas.optimization_sheets.</summary>
    public sealed class OptimizationSheet : ITenantOwned
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public Guid SessionId { get; set; }
        public int SheetNo { get; set; }
        public int StockWMm { get; set; }
        public int StockHMm { get; set; }
        public int ThicknessMm { get; set; }
        public string Color { get; set; } = null!;
        public string MaterialKey { get; set; } = null!;
        public int UsableX { get; set; }
        public int UsableY { get; set; }
        public int UsableW { get; set; }
        public int UsableH { get; set; }
        public decimal UsedSqm { get; set; }
        public decimal StockSqm { get; set; }
        public decimal UtilizationPct { get; set; }
        public int TrimCutCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public Guid? CreatedBy { get; set; }
        public Guid? UpdatedBy { get; set; }
        public OptimizationSession Session { get; set; } = null!;
    }
}

namespace ProGlass.Saas.Domain.Entities
{
    /// <summary>Maps to saas.optimization_placements.</summary>
    public sealed class OptimizationPlacement : ITenantOwned
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public Guid SheetId { get; set; }
        public string PieceRef { get; set; } = null!;
        public string JobRef { get; set; } = null!;
        public string GlassRef { get; set; } = null!;
        public string SpecLabel { get; set; } = null!;
        public string Position { get; set; } = null!;
        public string Color { get; set; } = null!;
        public int ThicknessMm { get; set; }
        public int XMm { get; set; }
        public int YMm { get; set; }
        public int WMm { get; set; }
        public int HMm { get; set; }
        public bool Rotated { get; set; }
        public int Qty { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public Guid? CreatedBy { get; set; }
        public Guid? UpdatedBy { get; set; }
        public OptimizationSheet Sheet { get; set; } = null!;
    }
}

namespace ProGlass.Saas.Domain.Entities
{
    /// <summary>Maps to saas.optimization_cuts.</summary>
    public sealed class OptimizationCut : ITenantOwned
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public Guid SheetId { get; set; }
        public int Seq { get; set; }
        public string Axis { get; set; } = null!;
        public int AtMm { get; set; }
        public int FromX { get; set; }
        public int FromY { get; set; }
        public int FromW { get; set; }
        public int FromH { get; set; }
        public int Stage { get; set; }
        public bool IsTrim { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public Guid? CreatedBy { get; set; }
        public Guid? UpdatedBy { get; set; }
        public OptimizationSheet Sheet { get; set; } = null!;
    }
}

namespace ProGlass.Saas.Domain.Entities
{
    /// <summary>Maps to saas.optimization_leftovers.</summary>
    public sealed class OptimizationLeftover : ITenantOwned
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public Guid SheetId { get; set; }
        public int XMm { get; set; }
        public int YMm { get; set; }
        public int WMm { get; set; }
        public int HMm { get; set; }
        public bool IsReusable { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public Guid? CreatedBy { get; set; }
        public Guid? UpdatedBy { get; set; }
        public OptimizationSheet Sheet { get; set; } = null!;
    }
}
