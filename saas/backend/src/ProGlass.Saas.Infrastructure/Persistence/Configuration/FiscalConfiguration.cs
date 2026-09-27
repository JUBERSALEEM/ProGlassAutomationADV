// ═══════════════════════════════════════════════════════════════════════
// GENERATED FILE — DO NOT EDIT BY HAND
// Source of truth: saas/database/migrations/*.sql (verified by
// saas/database/verify/apply_and_test.py against live PostgreSQL).
// Regenerate with: python3 saas/devbox/generate_ef_model.py
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ProGlass.Saas.Domain.Entities;

namespace ProGlass.Saas.Infrastructure.Persistence.Configuration
{
    public sealed class TaxInvoiceConfiguration : IEntityTypeConfiguration<TaxInvoice>
    {
        public void Configure(EntityTypeBuilder<TaxInvoice> builder)
        {
            builder.ToTable("tax_invoices", "saas");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id").HasColumnType("uuid").HasDefaultValueSql("gen_random_uuid()");
            builder.Property(x => x.TenantId).HasColumnName("tenant_id").HasColumnType("uuid");
            builder.Property(x => x.InvoiceNo).HasColumnName("invoice_no").HasColumnType("text");
            builder.Property(x => x.ProformaId).HasColumnName("proforma_id").HasColumnType("uuid");
            builder.Property(x => x.DeliveryNoteReference).HasColumnName("delivery_note_reference").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.JobOrderId).HasColumnName("job_order_id").HasColumnType("uuid");
            builder.Property(x => x.Status).HasColumnName("status").HasColumnType("text").HasDefaultValue("Issued");
            builder.Property(x => x.IssueDate).HasColumnName("issue_date").HasColumnType("date");
            builder.Property(x => x.DueDate).HasColumnName("due_date").HasColumnType("date");
            builder.Property(x => x.Subtotal).HasColumnName("subtotal").HasColumnType("numeric(18,2)").HasDefaultValue(0m);
            builder.Property(x => x.DiscountAmount).HasColumnName("discount_amount").HasColumnType("numeric(18,2)").HasDefaultValue(0m);
            builder.Property(x => x.VatAmount).HasColumnName("vat_amount").HasColumnType("numeric(18,2)").HasDefaultValue(0m);
            builder.Property(x => x.GrandTotal).HasColumnName("grand_total").HasColumnType("numeric(18,2)").HasDefaultValue(0m);
            builder.Property(x => x.PaidAmount).HasColumnName("paid_amount").HasColumnType("numeric(18,2)").HasDefaultValue(0m);
            builder.Property(x => x.BalanceAmount).HasColumnName("balance_amount").HasColumnType("numeric(18,2)").HasDefaultValue(0m);
            builder.Property(x => x.SnapshotJson).HasColumnName("snapshot_json").HasColumnType("jsonb");
            builder.Property(x => x.RowVersion).HasColumnName("row_version").HasColumnType("integer").HasDefaultValue(0);
            builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
            builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
            builder.Property(x => x.CreatedBy).HasColumnName("created_by").HasColumnType("uuid");
            builder.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid");
            builder.Property(x => x.CustomerName).HasColumnName("customer_name").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.ProjectName).HasColumnName("project_name").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.PaymentTerms).HasColumnName("payment_terms").HasColumnType("text").HasDefaultValue("Net 30");
            builder.Property(x => x.DiscountPercent).HasColumnName("discount_percent").HasColumnType("numeric(18,2)").HasDefaultValue(0m);
            builder.Property(x => x.IsVoid).HasColumnName("is_void").HasColumnType("boolean").HasDefaultValue(false);
            builder.Property(x => x.VoidedAt).HasColumnName("voided_at").HasColumnType("timestamptz");
            builder.Property(x => x.VoidReason).HasColumnName("void_reason").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.CompanyName).HasColumnName("company_name").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.CompanyTrn).HasColumnName("company_trn").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.PurchaseOrderNumber).HasColumnName("purchase_order_number").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.BankName).HasColumnName("bank_name").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.BankAccountNumber).HasColumnName("bank_account_number").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.Iban).HasColumnName("iban").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.SwiftCode).HasColumnName("swift_code").HasColumnType("text").HasDefaultValue("");
            builder.HasIndex(x => new { x.TenantId, x.InvoiceNo })
                .IsUnique().HasDatabaseName("uq_tax_invoices_tenant_no");
            builder.HasIndex(x => new { x.TenantId, x.Status })
                .HasDatabaseName("ix_tax_invoices_tenant_status");
            builder.HasIndex(x => new { x.TenantId, x.IssueDate })
                .HasDatabaseName("ix_tax_invoices_tenant_issue_date");
            builder.HasOne(x => x.Proforma)
                .WithMany()
                .HasForeignKey(x => x.ProformaId)
                .OnDelete(DeleteBehavior.SetNull);
            builder.HasOne(x => x.JobOrder)
                .WithMany()
                .HasForeignKey(x => x.JobOrderId)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }

    public sealed class CreditNoteConfiguration : IEntityTypeConfiguration<CreditNote>
    {
        public void Configure(EntityTypeBuilder<CreditNote> builder)
        {
            builder.ToTable("credit_notes", "saas");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id").HasColumnType("uuid").HasDefaultValueSql("gen_random_uuid()");
            builder.Property(x => x.TenantId).HasColumnName("tenant_id").HasColumnType("uuid");
            builder.Property(x => x.CreditNoteNo).HasColumnName("credit_note_no").HasColumnType("text");
            builder.Property(x => x.TaxInvoiceId).HasColumnName("tax_invoice_id").HasColumnType("uuid");
            builder.Property(x => x.Reason).HasColumnName("reason").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.Subtotal).HasColumnName("subtotal").HasColumnType("numeric(18,2)").HasDefaultValue(0m);
            builder.Property(x => x.VatAmount).HasColumnName("vat_amount").HasColumnType("numeric(18,2)").HasDefaultValue(0m);
            builder.Property(x => x.GrandTotal).HasColumnName("grand_total").HasColumnType("numeric(18,2)").HasDefaultValue(0m);
            builder.Property(x => x.Status).HasColumnName("status").HasColumnType("text").HasDefaultValue("draft");
            builder.Property(x => x.IssuedAt).HasColumnName("issued_at").HasColumnType("timestamptz");
            builder.Property(x => x.VoidedAt).HasColumnName("voided_at").HasColumnType("timestamptz");
            builder.Property(x => x.VoidedBy).HasColumnName("voided_by").HasColumnType("uuid");
            builder.Property(x => x.VoidReason).HasColumnName("void_reason").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.RowVersion).HasColumnName("row_version").HasColumnType("integer").HasDefaultValue(0);
            builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
            builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
            builder.Property(x => x.CreatedBy).HasColumnName("created_by").HasColumnType("uuid");
            builder.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid");
            builder.HasIndex(x => new { x.TenantId, x.CreditNoteNo })
                .IsUnique().HasDatabaseName("uq_credit_notes_tenant_no");
            builder.HasIndex(x => new { x.TenantId, x.Status })
                .HasDatabaseName("ix_credit_notes_tenant_status");
            builder.HasOne(x => x.TaxInvoice)
                .WithMany()
                .HasForeignKey(x => x.TaxInvoiceId)
                .OnDelete(DeleteBehavior.SetNull);
            builder.HasOne(x => x.VoidedByUser)
                .WithMany()
                .HasForeignKey(x => x.VoidedBy)
                .OnDelete(DeleteBehavior.SetNull);
        }
    }

    public sealed class StatementConfiguration : IEntityTypeConfiguration<Statement>
    {
        public void Configure(EntityTypeBuilder<Statement> builder)
        {
            builder.ToTable("statements", "saas");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id").HasColumnType("uuid").HasDefaultValueSql("gen_random_uuid()");
            builder.Property(x => x.TenantId).HasColumnName("tenant_id").HasColumnType("uuid");
            builder.Property(x => x.StatementNo).HasColumnName("statement_no").HasColumnType("text");
            builder.Property(x => x.Title).HasColumnName("title").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.FilterCompany).HasColumnName("filter_company").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.FilterCustomer).HasColumnName("filter_customer").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.FilterProject).HasColumnName("filter_project").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.FilterSalesman).HasColumnName("filter_salesman").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.FilterDateFrom).HasColumnName("filter_date_from").HasColumnType("date");
            builder.Property(x => x.FilterDateTo).HasColumnName("filter_date_to").HasColumnType("date");
            builder.Property(x => x.GeneratedAt).HasColumnName("generated_at").HasColumnType("timestamptz");
            builder.Property(x => x.TotalsJson).HasColumnName("totals_json").HasColumnType("jsonb");
            builder.Property(x => x.StorageKey).HasColumnName("storage_key").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
            builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
            builder.Property(x => x.CreatedBy).HasColumnName("created_by").HasColumnType("uuid");
            builder.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid");
            builder.Property(x => x.FilterStatuses).HasColumnName("filter_statuses").HasColumnType("jsonb");
            builder.HasIndex(x => new { x.TenantId, x.StatementNo })
                .IsUnique().HasDatabaseName("uq_statements_tenant_no");
            builder.HasIndex(x => new { x.TenantId, x.GeneratedAt })
                .HasDatabaseName("ix_statements_tenant_generated");
        }
    }

    public sealed class StatementLineConfiguration : IEntityTypeConfiguration<StatementLine>
    {
        public void Configure(EntityTypeBuilder<StatementLine> builder)
        {
            builder.ToTable("statement_lines", "saas");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).HasColumnName("id").HasColumnType("uuid").HasDefaultValueSql("gen_random_uuid()");
            builder.Property(x => x.TenantId).HasColumnName("tenant_id").HasColumnType("uuid");
            builder.Property(x => x.StatementId).HasColumnName("statement_id").HasColumnType("uuid");
            builder.Property(x => x.LineType).HasColumnName("line_type").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.DocumentNo).HasColumnName("document_no").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.DocumentDate).HasColumnName("document_date").HasColumnType("date");
            builder.Property(x => x.Description).HasColumnName("description").HasColumnType("text").HasDefaultValue("");
            builder.Property(x => x.Debit).HasColumnName("debit").HasColumnType("numeric(18,2)").HasDefaultValue(0m);
            builder.Property(x => x.Credit).HasColumnName("credit").HasColumnType("numeric(18,2)").HasDefaultValue(0m);
            builder.Property(x => x.RunningBalance).HasColumnName("running_balance").HasColumnType("numeric(18,2)").HasDefaultValue(0m);
            builder.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
            builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").HasDefaultValueSql("now()");
            builder.Property(x => x.CreatedBy).HasColumnName("created_by").HasColumnType("uuid");
            builder.Property(x => x.UpdatedBy).HasColumnName("updated_by").HasColumnType("uuid");
            builder.Property(x => x.LineNo).HasColumnName("line_no").HasColumnType("integer").HasDefaultValue(0);
            builder.HasIndex(x => new { x.TenantId, x.StatementId })
                .HasDatabaseName("ix_statement_lines_tenant_statement");
            builder.HasOne(x => x.Statement)
                .WithMany()
                .HasForeignKey(x => x.StatementId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }

}