// ═══════════════════════════════════════════════════════════════════════
// GENERATED FILE — DO NOT EDIT BY HAND
// Source of truth: saas/database/migrations/*.sql (verified by
// saas/database/verify/apply_and_test.py against live PostgreSQL).
// Regenerate with: python3 saas/devbox/generate_ef_model.py
// ═══════════════════════════════════════════════════════════════════════

using ProGlass.Saas.Domain.Tenancy;

namespace ProGlass.Saas.Domain.Entities
{
    /// <summary>Maps to saas.tax_invoices.</summary>
    public sealed class TaxInvoice : ITenantOwned
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public string InvoiceNo { get; set; } = null!;
        public Guid? ProformaId { get; set; }
        public string DeliveryNoteReference { get; set; } = null!;
        public Guid? JobOrderId { get; set; }
        public string Status { get; set; } = null!;
        public DateOnly? IssueDate { get; set; }
        public DateOnly? DueDate { get; set; }
        public decimal Subtotal { get; set; }
        public decimal DiscountAmount { get; set; }
        public decimal VatAmount { get; set; }
        public decimal GrandTotal { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal BalanceAmount { get; set; }
        public string? SnapshotJson { get; set; }
        public int RowVersion { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public Guid? CreatedBy { get; set; }
        public Guid? UpdatedBy { get; set; }
        public string CustomerName { get; set; } = null!;
        public string ProjectName { get; set; } = null!;
        public string PaymentTerms { get; set; } = null!;
        public decimal DiscountPercent { get; set; }
        public bool IsVoid { get; set; }
        public DateTime? VoidedAt { get; set; }
        public string VoidReason { get; set; } = null!;
        public string CompanyName { get; set; } = null!;
        public string CompanyTrn { get; set; } = null!;
        public string PurchaseOrderNumber { get; set; } = null!;
        public string BankName { get; set; } = null!;
        public string BankAccountNumber { get; set; } = null!;
        public string Iban { get; set; } = null!;
        public string SwiftCode { get; set; } = null!;
        public ProformaInvoice? Proforma { get; set; } = null;
        public JobOrder? JobOrder { get; set; } = null;
    }
}

namespace ProGlass.Saas.Domain.Entities
{
    /// <summary>Maps to saas.tax_invoice_items.</summary>
    public sealed class TaxInvoiceItem : ITenantOwned
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public Guid TaxInvoiceId { get; set; }
        public Guid? SpecGroupItemId { get; set; }
        public int SpecGroupNumber { get; set; }
        public int SerialNumber { get; set; }
        public string SpecGroupName { get; set; } = null!;
        public string GlassType { get; set; } = null!;
        public string GlassRef { get; set; } = null!;
        public int W1Mm { get; set; }
        public int H1Mm { get; set; }
        public int W2Mm { get; set; }
        public int H2Mm { get; set; }
        public int Qty { get; set; }
        public decimal Sqm { get; set; }
        public decimal TotalSqm { get; set; }
        public decimal Rate { get; set; }
        public decimal Value { get; set; }
        public string Notes { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public Guid? CreatedBy { get; set; }
        public Guid? UpdatedBy { get; set; }
        public TaxInvoice TaxInvoice { get; set; } = null!;
        public SpecGroupItem? SpecGroupItem { get; set; } = null;
    }
}

namespace ProGlass.Saas.Domain.Entities
{
    /// <summary>Maps to saas.credit_notes.</summary>
    public sealed class CreditNote : ITenantOwned
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public string CreditNoteNo { get; set; } = null!;
        public Guid? TaxInvoiceId { get; set; }
        public string Reason { get; set; } = null!;
        public decimal Subtotal { get; set; }
        public decimal VatAmount { get; set; }
        public decimal GrandTotal { get; set; }
        public string Status { get; set; } = null!;
        public DateTime? IssuedAt { get; set; }
        public DateTime? VoidedAt { get; set; }
        public Guid? VoidedBy { get; set; }
        public string VoidReason { get; set; } = null!;
        public int RowVersion { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public Guid? CreatedBy { get; set; }
        public Guid? UpdatedBy { get; set; }
        public TaxInvoice? TaxInvoice { get; set; } = null;
        public User? VoidedByUser { get; set; } = null;
    }
}

namespace ProGlass.Saas.Domain.Entities
{
    /// <summary>Maps to saas.statements.</summary>
    public sealed class Statement : ITenantOwned
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public string StatementNo { get; set; } = null!;
        public string Title { get; set; } = null!;
        public string FilterCompany { get; set; } = null!;
        public string FilterCustomer { get; set; } = null!;
        public string FilterProject { get; set; } = null!;
        public string FilterSalesman { get; set; } = null!;
        public DateOnly? FilterDateFrom { get; set; }
        public DateOnly? FilterDateTo { get; set; }
        public DateTime? GeneratedAt { get; set; }
        public string? TotalsJson { get; set; }
        public string StorageKey { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public Guid? CreatedBy { get; set; }
        public Guid? UpdatedBy { get; set; }
        public string? FilterStatuses { get; set; }
    }
}

namespace ProGlass.Saas.Domain.Entities
{
    /// <summary>Maps to saas.statement_lines.</summary>
    public sealed class StatementLine : ITenantOwned
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public Guid StatementId { get; set; }
        public string LineType { get; set; } = null!;
        public string DocumentNo { get; set; } = null!;
        public DateOnly? DocumentDate { get; set; }
        public string Description { get; set; } = null!;
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        public decimal RunningBalance { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public Guid? CreatedBy { get; set; }
        public Guid? UpdatedBy { get; set; }
        public int LineNo { get; set; }
        public Statement Statement { get; set; } = null!;
    }
}
