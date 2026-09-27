// ═══════════════════════════════════════════════════════════════════════
// GENERATED FILE — DO NOT EDIT BY HAND
// Source of truth: saas/database/migrations/*.sql (verified by
// saas/database/verify/apply_and_test.py against live PostgreSQL).
// Regenerate with: python3 saas/devbox/generate_ef_model.py
// ═══════════════════════════════════════════════════════════════════════

using ProGlass.Saas.Domain.Tenancy;

namespace ProGlass.Saas.Domain.Entities
{
    /// <summary>Maps to saas.inquiries.</summary>
    public sealed class Inquiry : ITenantOwned
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public string InquiryNo { get; set; } = null!;
        public string Status { get; set; } = null!;
        public string StatusText { get; set; } = null!;
        public string Priority { get; set; } = null!;
        public Guid? CustomerId { get; set; }
        public Guid? ProjectId { get; set; }
        public string CustomerName { get; set; } = null!;
        public string ProjectName { get; set; } = null!;
        public string Salesman { get; set; } = null!;
        public string Notes { get; set; } = null!;
        public DateTime? ReceivedAt { get; set; }
        public string? BoqJson { get; set; }
        public Guid? ConvertedProformaId { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public Guid? CreatedBy { get; set; }
        public Guid? UpdatedBy { get; set; }
        public Client? Customer { get; set; } = null;
        public Project? Project { get; set; } = null;
    }
}

namespace ProGlass.Saas.Domain.Entities
{
    /// <summary>Maps to saas.attachments.</summary>
    public sealed class Attachment : ITenantOwned
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public string EntityType { get; set; } = null!;
        public Guid EntityId { get; set; }
        public string StorageKey { get; set; } = null!;
        public string OriginalName { get; set; } = null!;
        public string MimeType { get; set; } = null!;
        public long SizeBytes { get; set; }
        public Guid? UploadedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public User? UploadedByUser { get; set; } = null;
    }
}

namespace ProGlass.Saas.Domain.Entities
{
    /// <summary>Maps to saas.specifications.</summary>
    public sealed class Specification : ITenantOwned
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public string SpecName { get; set; } = null!;
        public string GlassType { get; set; } = null!;
        public string? ParamsJson { get; set; }
        public decimal RatePerSqm { get; set; }
        public decimal CostBasisPerSqm { get; set; }
        public int UsageCount { get; set; }
        public DateTime? LastUsedAt { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public Guid? CreatedBy { get; set; }
        public Guid? UpdatedBy { get; set; }
        public string? Category { get; set; }
        public string? Color { get; set; }
        public int ThicknessMm { get; set; }
        public int SheetWidthMm { get; set; }
        public int SheetHeightMm { get; set; }
        public Guid? SheetId { get; set; }
        public string? PanesJson { get; set; }
        public string? LastProformaNo { get; set; }
    }
}

namespace ProGlass.Saas.Domain.Entities
{
    /// <summary>Maps to saas.tenant_pricing_config.</summary>
    public sealed class TenantPricingConfig : ITenantOwned
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public decimal YieldFactorDefault { get; set; }
        public decimal MarginDefault { get; set; }
        public string ThicknessOptions { get; set; } = null!;
        public string TreatmentOptions { get; set; } = null!;
        public string AspWidths { get; set; } = null!;
        public string InterlayerTypes { get; set; } = null!;
        public string InterlayerThicknesses { get; set; } = null!;
        public string PolishOptions { get; set; } = null!;
        public string PanelExtraOptions { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public Guid? CreatedBy { get; set; }
        public Guid? UpdatedBy { get; set; }
    }
}

namespace ProGlass.Saas.Domain.Entities
{
    /// <summary>Maps to saas.tenant_vat_config.</summary>
    public sealed class TenantVatConfig : ITenantOwned
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public decimal VatRate { get; set; }
        public string Currency { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public Guid? CreatedBy { get; set; }
        public Guid? UpdatedBy { get; set; }
    }
}

namespace ProGlass.Saas.Domain.Entities
{
    /// <summary>Maps to saas.proforma_invoices.</summary>
    public sealed class ProformaInvoice : ITenantOwned
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public string ProformaNo { get; set; } = null!;
        public Guid? RootProformaId { get; set; }
        public int RevisionNumber { get; set; }
        public Guid? InquiryId { get; set; }
        public Guid? CompanyId { get; set; }
        public Guid? ClientId { get; set; }
        public Guid? ProjectId { get; set; }
        public Guid? SalesmanId { get; set; }
        public string Status { get; set; } = null!;
        public string StatusText { get; set; } = null!;
        public string LifecycleState { get; set; } = null!;
        public DateOnly? IssueDate { get; set; }
        public DateOnly? ValidUntil { get; set; }
        public decimal GlassMaterialsSubtotal { get; set; }
        public decimal OtherChargesSubtotal { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal VatAmount { get; set; }
        public decimal GrandTotalVatInclusive { get; set; }
        public bool IsVoid { get; set; }
        public DateTime? VoidedAt { get; set; }
        public Guid? VoidedBy { get; set; }
        public string VoidReason { get; set; } = null!;
        public int FurthestStep { get; set; }
        public string? SnapshotJson { get; set; }
        public int RowVersion { get; set; }
        public DateTime? SentAt { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public DateTime? ConfirmedAt { get; set; }
        public Guid? JobOrderId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public Guid? CreatedBy { get; set; }
        public Guid? UpdatedBy { get; set; }
        public ProformaInvoice? RootProforma { get; set; } = null;
        public Inquiry? Inquiry { get; set; } = null;
        public Company? Company { get; set; } = null;
        public Client? Client { get; set; } = null;
        public Project? Project { get; set; } = null;
        public Salesman? Salesman { get; set; } = null;
    }
}

namespace ProGlass.Saas.Domain.Entities
{
    /// <summary>Maps to saas.specification_usages.</summary>
    public sealed class SpecificationUsage : ITenantOwned
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public Guid SpecId { get; set; }
        public Guid ProformaId { get; set; }
        public string SpecName { get; set; } = null!;
        public string GlassType { get; set; } = null!;
        public string CompanyName { get; set; } = null!;
        public string CustomerName { get; set; } = null!;
        public string ProjectName { get; set; } = null!;
        public decimal PricePerSqm { get; set; }
        public int SheetWidthMm { get; set; }
        public int SheetHeightMm { get; set; }
        public DateTime UsedAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public Specification Spec { get; set; } = null!;
        public ProformaInvoice Proforma { get; set; } = null!;
    }
}

namespace ProGlass.Saas.Domain.Entities
{
    /// <summary>Maps to saas.spec_groups.</summary>
    public sealed class SpecGroup : ITenantOwned
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public Guid ProformaId { get; set; }
        public int GroupNumber { get; set; }
        public string GlassType { get; set; } = null!;
        public string SpecName { get; set; } = null!;
        public Guid? SpecId { get; set; }
        public decimal RatePerSqm { get; set; }
        public string PriceSource { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public Guid? CreatedBy { get; set; }
        public Guid? UpdatedBy { get; set; }
        public ProformaInvoice Proforma { get; set; } = null!;
        public Specification? Spec { get; set; } = null;
    }
}

namespace ProGlass.Saas.Domain.Entities
{
    /// <summary>Maps to saas.spec_group_items.</summary>
    public sealed class SpecGroupItem : ITenantOwned
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public Guid SpecGroupId { get; set; }
        public int RowSerial { get; set; }
        public string Position { get; set; } = null!;
        public int WidthMm { get; set; }
        public int HeightMm { get; set; }
        public int Qty { get; set; }
        public int W1 { get; set; }
        public int H1 { get; set; }
        public int W2 { get; set; }
        public int H2 { get; set; }
        public decimal Sqm { get; set; }
        public decimal TotalSqm { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Total { get; set; }
        public string Notes { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public Guid? CreatedBy { get; set; }
        public Guid? UpdatedBy { get; set; }
        public SpecGroup SpecGroup { get; set; } = null!;
    }
}

namespace ProGlass.Saas.Domain.Entities
{
    /// <summary>Maps to saas.other_charges.</summary>
    public sealed class OtherCharge : ITenantOwned
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public Guid ProformaId { get; set; }
        public string ChargeName { get; set; } = null!;
        public string MetricKey { get; set; } = null!;
        public string CheckedSpecGroupNumbers { get; set; } = null!;
        public decimal Rate { get; set; }
        public decimal Amount { get; set; }
        public bool IsActive { get; set; }
        public int SortOrder { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public Guid? CreatedBy { get; set; }
        public Guid? UpdatedBy { get; set; }
        public ProformaInvoice Proforma { get; set; } = null!;
    }
}

namespace ProGlass.Saas.Domain.Entities
{
    /// <summary>Maps to saas.wastage_rows.</summary>
    public sealed class WastageRow : ITenantOwned
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public Guid ProformaId { get; set; }
        public Guid? SpecGroupId { get; set; }
        public string ColorName { get; set; } = null!;
        public decimal StockSqm { get; set; }
        public decimal GlassSqm { get; set; }
        public decimal OptimizationPercent { get; set; }
        public int SheetQuantity { get; set; }
        public decimal FormulaPricePerSqm { get; set; }
        public decimal Rate { get; set; }
        public decimal ResultAmount { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public Guid? CreatedBy { get; set; }
        public Guid? UpdatedBy { get; set; }
        public ProformaInvoice Proforma { get; set; } = null!;
        public SpecGroup? SpecGroup { get; set; } = null;
    }
}

namespace ProGlass.Saas.Domain.Entities
{
    /// <summary>Maps to saas.job_orders.</summary>
    public sealed class JobOrder : ITenantOwned
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public string JobNo { get; set; } = null!;
        public Guid? ProformaId { get; set; }
        public string Status { get; set; } = null!;
        public string StatusText { get; set; } = null!;
        public Guid? CustomerId { get; set; }
        public Guid? ProjectId { get; set; }
        public Guid? SalesmanId { get; set; }
        public DateOnly? DueDate { get; set; }
        public string Notes { get; set; } = null!;
        public int RowVersion { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public Guid? CreatedBy { get; set; }
        public Guid? UpdatedBy { get; set; }
        public ProformaInvoice? Proforma { get; set; } = null;
        public Client? Customer { get; set; } = null;
        public Project? Project { get; set; } = null;
        public Salesman? Salesman { get; set; } = null;
    }
}

namespace ProGlass.Saas.Domain.Entities
{
    /// <summary>Maps to saas.production_pieces.</summary>
    public sealed class ProductionPiece : ITenantOwned
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public Guid JobOrderId { get; set; }
        public string PieceLabel { get; set; } = null!;
        public Guid? SpecGroupItemId { get; set; }
        public string Stage { get; set; } = null!;
        public string Status { get; set; } = null!;
        public int WidthMm { get; set; }
        public int HeightMm { get; set; }
        public int Qty { get; set; }
        public Guid? OptimizationSheetId { get; set; }
        public string Notes { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public Guid? CreatedBy { get; set; }
        public Guid? UpdatedBy { get; set; }
        public JobOrder JobOrder { get; set; } = null!;
        public SpecGroupItem? SpecGroupItem { get; set; } = null;
    }
}

namespace ProGlass.Saas.Domain.Entities
{
    /// <summary>Maps to saas.production_events.</summary>
    public sealed class ProductionEvent : ITenantOwned
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public Guid ProductionPieceId { get; set; }
        public string EventType { get; set; } = null!;
        public string? Payload { get; set; }
        public DateTime CreatedAt { get; set; }
        public ProductionPiece ProductionPiece { get; set; } = null!;
    }
}

namespace ProGlass.Saas.Domain.Entities
{
    /// <summary>Maps to saas.material_reservations.</summary>
    public sealed class MaterialReservation : ITenantOwned
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public Guid JobOrderId { get; set; }
        public Guid GlassSheetId { get; set; }
        public int ReservedQty { get; set; }
        public string Status { get; set; } = null!;
        public Guid? ReservedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public JobOrder JobOrder { get; set; } = null!;
        public GlassSheet GlassSheet { get; set; } = null!;
        public User? ReservedByUser { get; set; } = null;
    }
}

namespace ProGlass.Saas.Domain.Entities
{
    /// <summary>Maps to saas.delivery_notes.</summary>
    public sealed class DeliveryNote : ITenantOwned
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public string DeliveryNo { get; set; } = null!;
        public Guid? JobOrderId { get; set; }
        public string Status { get; set; } = null!;
        public string StatusText { get; set; } = null!;
        public Guid? CustomerId { get; set; }
        public Guid? ProjectId { get; set; }
        public DateOnly? DeliveryDate { get; set; }
        public string Driver { get; set; } = null!;
        public string Vehicle { get; set; } = null!;
        public string Notes { get; set; } = null!;
        public int RowVersion { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public Guid? CreatedBy { get; set; }
        public Guid? UpdatedBy { get; set; }
        public JobOrder? JobOrder { get; set; } = null;
        public Client? Customer { get; set; } = null;
        public Project? Project { get; set; } = null;
    }
}

namespace ProGlass.Saas.Domain.Entities
{
    /// <summary>Maps to saas.delivery_note_items.</summary>
    public sealed class DeliveryNoteItem : ITenantOwned
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public Guid DeliveryNoteId { get; set; }
        public Guid? SpecGroupItemId { get; set; }
        public string Description { get; set; } = null!;
        public int WidthMm { get; set; }
        public int HeightMm { get; set; }
        public int Qty { get; set; }
        public decimal Sqm { get; set; }
        public int DeliveredQty { get; set; }
        public int ReturnedQty { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public Guid? CreatedBy { get; set; }
        public Guid? UpdatedBy { get; set; }
        public DeliveryNote DeliveryNote { get; set; } = null!;
        public SpecGroupItem? SpecGroupItem { get; set; } = null;
    }
}
