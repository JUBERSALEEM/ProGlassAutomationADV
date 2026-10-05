// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Audit/TaxInvoiceDeletePermissionTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// ERP lifecycle hardening (Phase 3, G-6). Deleting or voiding a fiscal
// document (a Tax Invoice) is a Manager/Admin-only capability. An Accountant
// may create, edit, issue and export invoices — but must NOT be able to
// erase or void a posted one. These tests pin that role→permission mapping so
// a future refactor can't silently hand the fiscal-delete capability to the
// wrong role.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Audit;
using Xunit;

namespace ProGlassAutomation.Tests.Audit
{
    public class TaxInvoiceDeletePermissionTests
    {
        [Theory]
        [InlineData(AppRoles.Administrator)]
        [InlineData(AppRoles.Manager)]
        public void Manager_and_admin_can_delete_tax_invoices(string role)
        {
            Assert.True(RolePermissions.Has(role, AppPermissions.TaxInvoiceDelete));
        }

        [Theory]
        [InlineData(AppRoles.Accountant)]
        [InlineData(AppRoles.Sales)]
        [InlineData(AppRoles.Estimator)]
        [InlineData(AppRoles.Production)]
        [InlineData(AppRoles.Store)]
        [InlineData(AppRoles.Viewer)]
        public void Non_managers_cannot_delete_tax_invoices(string role)
        {
            Assert.False(RolePermissions.Has(role, AppPermissions.TaxInvoiceDelete));
        }

        [Fact]
        public void Accountant_can_still_edit_invoices_but_not_delete_them()
        {
            // The accountant keeps the day-to-day fiscal capability …
            Assert.True(RolePermissions.Has(AppRoles.Accountant, AppPermissions.TaxInvoiceEdit));
            // … but not the destructive one.
            Assert.False(RolePermissions.Has(AppRoles.Accountant, AppPermissions.TaxInvoiceDelete));
        }

        [Fact]
        public void Delete_is_distinct_from_edit()
        {
            Assert.NotEqual(AppPermissions.TaxInvoiceEdit, AppPermissions.TaxInvoiceDelete);
        }
    }
}
