using ProGlassAutomation.Services.Audit;
using Xunit;

namespace ProGlassAutomation.Tests.Audit
{
    public class TaxInvoiceDnLinkPermissionTests
    {
        [Theory]
        [InlineData(AppRoles.Administrator)]
        [InlineData(AppRoles.Manager)]
        [InlineData(AppRoles.Accountant)]
        public void Finance_roles_can_manage_ti_owned_dn_links(string role)
        {
            Assert.True(RolePermissions.Has(role, AppPermissions.DeliveryNoteInvoiceLink));
        }

        [Fact]
        public void Accountant_link_permission_does_not_grant_delivery_note_body_edit()
        {
            Assert.True(RolePermissions.Has(
                AppRoles.Accountant, AppPermissions.DeliveryNoteInvoiceLink));
            Assert.False(RolePermissions.Has(
                AppRoles.Accountant, AppPermissions.DeliveryNoteEdit));
        }

        [Fact]
        public void Production_can_edit_delivery_notes_but_cannot_write_finance_links()
        {
            Assert.True(RolePermissions.Has(
                AppRoles.Production, AppPermissions.DeliveryNoteEdit));
            Assert.False(RolePermissions.Has(
                AppRoles.Production, AppPermissions.DeliveryNoteInvoiceLink));
        }

        [Fact]
        public void Viewer_cannot_write_invoice_links()
        {
            Assert.False(RolePermissions.Has(
                AppRoles.Viewer, AppPermissions.DeliveryNoteInvoiceLink));
        }
    }
}
