// ═══════════════════════════════════════════════════════════════════════
// PARITY TESTS — every assertion below was cross-checked against the WPF
// source of truth (Services/Audit/Permissions.cs, read 2026-09-27).
// If one of these tests fails after an intentional catalog change, the
// WPF file, this test AND the audit doc must change together.
// ═══════════════════════════════════════════════════════════════════════

using ProGlass.Saas.Domain.Authorization;
using Xunit;

namespace ProGlass.Saas.Domain.Tests;

public class RolePermissionsTests
{
    [Fact]
    public void Administrator_carries_the_wildcard_only()
    {
        var set = RolePermissions.For(AppRoles.Administrator);
        Assert.Single(set, AppPermissions.All);
    }

    [Fact]
    public void Administrator_satisfies_any_permission_via_wildcard()
    {
        Assert.True(RolePermissions.Has(AppRoles.Administrator, "Anything.AtAll"));
        Assert.True(RolePermissions.Has(AppRoles.Administrator, AppPermissions.TaxInvoiceDelete));
    }

    [Fact]
    public void Manager_can_delete_fiscal_documents_but_is_not_administrator()
    {
        Assert.True(RolePermissions.Has(AppRoles.Manager, AppPermissions.TaxInvoiceDelete));
        Assert.True(RolePermissions.Has(AppRoles.Manager, AppPermissions.CreditNoteDelete));
        Assert.True(RolePermissions.Has(AppRoles.Manager, AppPermissions.ProformaDelete));
        Assert.True(RolePermissions.Has(AppRoles.Manager, AppPermissions.SheetStoreCreate));
        Assert.True(RolePermissions.Has(AppRoles.Manager, AppPermissions.StatementGenerate));
        // Manager is a concrete set, not the wildcard:
        Assert.DoesNotContain(AppPermissions.All, RolePermissions.For(AppRoles.Manager));
    }

    [Fact]
    public void Estimator_prices_and_estimates_but_never_touches_fiscal_delete()
    {
        Assert.True(RolePermissions.Has(AppRoles.Estimator, AppPermissions.ProformaCreate));
        Assert.True(RolePermissions.Has(AppRoles.Estimator, AppPermissions.ProformaEdit));
        Assert.True(RolePermissions.Has(AppRoles.Estimator, AppPermissions.InquiryEdit));
        Assert.True(RolePermissions.Has(AppRoles.Estimator, AppPermissions.SheetStoreView));
        Assert.False(RolePermissions.Has(AppRoles.Estimator, AppPermissions.TaxInvoiceDelete));
        Assert.False(RolePermissions.Has(AppRoles.Estimator, AppPermissions.JobOrderEdit));
    }

    [Fact]
    public void Sales_sees_deliveries_and_invoices_but_cannot_edit_deliveries()
    {
        Assert.True(RolePermissions.Has(AppRoles.Sales, AppPermissions.DeliveryNoteView));
        Assert.True(RolePermissions.Has(AppRoles.Sales, AppPermissions.TaxInvoiceView));
        Assert.False(RolePermissions.Has(AppRoles.Sales, AppPermissions.DeliveryNoteEdit));
        Assert.False(RolePermissions.Has(AppRoles.Sales, AppPermissions.TaxInvoiceDelete));
    }

    [Fact]
    public void Production_runs_the_floor()
    {
        Assert.True(RolePermissions.Has(AppRoles.Production, AppPermissions.JobOrderEdit));
        Assert.True(RolePermissions.Has(AppRoles.Production, AppPermissions.DeliveryNoteEdit));
        Assert.True(RolePermissions.Has(AppRoles.Production, AppPermissions.SheetStoreView));
        Assert.False(RolePermissions.Has(AppRoles.Production, AppPermissions.TaxInvoiceView));
    }

    [Fact]
    public void Store_owns_sheet_store_only()
    {
        Assert.True(RolePermissions.Has(AppRoles.Store, AppPermissions.SheetStoreView));
        Assert.True(RolePermissions.Has(AppRoles.Store, AppPermissions.SheetStoreCreate));
        Assert.True(RolePermissions.Has(AppRoles.Store, AppPermissions.SheetStoreEdit));
        Assert.True(RolePermissions.Has(AppRoles.Store, AppPermissions.SheetStoreDelete));
        Assert.True(RolePermissions.Has(AppRoles.Store, AppPermissions.DeliveryNoteView));
        Assert.False(RolePermissions.Has(AppRoles.Store, AppPermissions.JobOrderEdit));
        Assert.False(RolePermissions.Has(AppRoles.Store, AppPermissions.ProformaCreate));
    }

    [Fact]
    public void Accountant_edits_and_exports_fiscal_docs_but_cannot_delete_posted_ones()
    {
        // WPF Phase 3 G-6 rule — the headline fiscal-integrity grant:
        Assert.True(RolePermissions.Has(AppRoles.Accountant, AppPermissions.TaxInvoiceEdit));
        Assert.True(RolePermissions.Has(AppRoles.Accountant, AppPermissions.TaxInvoiceExport));
        Assert.True(RolePermissions.Has(AppRoles.Accountant, AppPermissions.CreditNoteEdit));
        Assert.True(RolePermissions.Has(AppRoles.Accountant, AppPermissions.StatementGenerate));
        Assert.False(RolePermissions.Has(AppRoles.Accountant, AppPermissions.TaxInvoiceDelete));
        Assert.False(RolePermissions.Has(AppRoles.Accountant, AppPermissions.CreditNoteDelete));
        Assert.False(RolePermissions.Has(AppRoles.Accountant, AppPermissions.ProformaCreate));
    }

    [Fact]
    public void Viewer_reads_everything_and_writes_nothing()
    {
        Assert.True(RolePermissions.Has(AppRoles.Viewer, AppPermissions.ProformaView));
        Assert.True(RolePermissions.Has(AppRoles.Viewer, AppPermissions.AuditView));
        Assert.True(RolePermissions.Has(AppRoles.Viewer, AppPermissions.ReportsView));
        Assert.False(RolePermissions.Has(AppRoles.Viewer, AppPermissions.ProformaCreate));
        Assert.False(RolePermissions.Has(AppRoles.Viewer, AppPermissions.SheetStoreEdit));
        // Viewer's set is exactly the WPF ViewAll() bundle:
        Assert.Equal(
            new[]
            {
                AppPermissions.DashboardView, AppPermissions.ProformaView,
                AppPermissions.JobOrderView, AppPermissions.DeliveryNoteView,
                AppPermissions.TaxInvoiceView, AppPermissions.CreditNoteView,
                AppPermissions.SheetStoreView,
                AppPermissions.ReportsView, AppPermissions.LogsView,
                AppPermissions.AuditView
            },
            RolePermissions.For(AppRoles.Viewer));
    }

    [Fact]
    public void Legacy_role_names_keep_working()
    {
        // "Operator" (legacy) maps to the Store set (WPF static ctor rule):
        Assert.Equal(RolePermissions.For(AppRoles.Store), RolePermissions.For(AppRoles.Operator));
        // "Admin" (legacy alias) maps to Administrator:
        Assert.Equal(
            RolePermissions.For(AppRoles.Administrator), RolePermissions.For("Admin"));
    }

    [Fact]
    public void Unknown_role_grants_nothing()
    {
        Assert.Empty(RolePermissions.For("Wizard"));
        Assert.Empty(RolePermissions.For(null));
        Assert.False(RolePermissions.Has("Wizard", AppPermissions.DashboardView));
    }

    [Fact]
    public void Role_catalog_matches_WPF_list()
    {
        Assert.Equal(
            new[]
            {
                AppRoles.Administrator, AppRoles.Manager, AppRoles.Estimator, AppRoles.Sales,
                AppRoles.Production, AppRoles.Store, AppRoles.Accountant, AppRoles.Viewer
            },
            AppRoles.All);
    }
}
