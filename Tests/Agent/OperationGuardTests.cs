// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/OperationGuardTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// Covers audit finding A-1: authorization existed in 18 ViewModels and
// ZERO services, while 34 mutating repository methods could be reached
// directly. The brief's rule — "never rely only on hiding a button" —
// was not implemented.
//
// OperationGuard is the headless service-layer gate. These tests pin the
// two properties that make it a real boundary rather than decoration:
//
//   1. It FAILS CLOSED. No signed-in user ⇒ deny. Absence of identity
//      must never mean absence of restriction.
//   2. It cannot be silently disabled by anything the end user or a
//      config file can reach — only the test hook flips it.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Audit;
using System;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    [Collection("OperationGuard")]
    public class OperationGuardTests : IDisposable
    {
        private readonly bool _original;

        public OperationGuardTests()
        {
            _original = OperationGuard.Enabled;
            OperationGuard.Enabled = true;
        }

        public void Dispose() => OperationGuard.Enabled = _original;

        /// <summary>
        /// No AuditService session is initialised in a unit-test process,
        /// so CurrentUser is null and every permission must deny. This is
        /// the single most important property of the guard: a code path
        /// that reaches a repository without an authenticated user must
        /// not be allowed to write.
        /// </summary>
        [Fact]
        public void Denies_when_no_user_is_signed_in()
        {
            var ex = Assert.Throws<PermissionDeniedException>(() =>
                OperationGuard.Demand(
                    AppPermissions.SheetStoreDelete, "delete a glass sheet"));

            Assert.Equal(AppPermissions.SheetStoreDelete, ex.Permission);
            Assert.Equal("delete a glass sheet", ex.Operation);
        }

        [Fact]
        public void IsAllowed_is_false_when_no_user_is_signed_in()
        {
            Assert.False(OperationGuard.IsAllowed(AppPermissions.ProformaDelete));
        }

        /// <summary>
        /// The message shown to the user must explain what was blocked
        /// without leaking internals such as stack traces or file paths.
        /// </summary>
        [Fact]
        public void User_message_is_specific_and_safe()
        {
            var ex = new PermissionDeniedException(
                AppPermissions.TaxInvoiceEdit, "delete a tax invoice", "Viewer");

            Assert.Contains("delete a tax invoice", ex.UserMessage);
            Assert.Contains(AppPermissions.TaxInvoiceEdit, ex.UserMessage);
            Assert.Contains("Viewer", ex.UserMessage);

            Assert.DoesNotContain("Exception", ex.UserMessage);
            Assert.DoesNotContain(@"\", ex.UserMessage);
        }

        [Fact]
        public void Missing_role_renders_as_none_not_empty()
        {
            var ex = new PermissionDeniedException(
                AppPermissions.ProformaCreate, "create an invoice", "");

            Assert.Contains("none", ex.UserMessage);
        }

        /// <summary>
        /// The test hook must actually bypass, otherwise repository tests
        /// cannot run headless.
        /// </summary>
        [Fact]
        public void Disabled_guard_allows_the_operation()
        {
            OperationGuard.Enabled = false;
            try
            {
                OperationGuard.Demand(
                    AppPermissions.SheetStoreDelete, "delete a glass sheet");
                Assert.True(OperationGuard.IsAllowed(AppPermissions.SheetStoreDelete));
            }
            finally
            {
                OperationGuard.Enabled = true;
            }
        }

        // ── Role → permission mapping ─────────────────────────────────
        // RolePermissions is pure and testable without a session.

        [Fact]
        public void Administrator_has_every_permission()
        {
            Assert.True(RolePermissions.Has(
                AppRoles.Administrator, AppPermissions.SheetStoreDelete));
            Assert.True(RolePermissions.Has(
                AppRoles.Administrator, AppPermissions.SettingsManage));
        }

        [Fact]
        public void Viewer_cannot_write_anything()
        {
            Assert.False(RolePermissions.Has(
                AppRoles.Viewer, AppPermissions.ProformaDelete));
            Assert.False(RolePermissions.Has(
                AppRoles.Viewer, AppPermissions.SheetStoreEdit));
            Assert.False(RolePermissions.Has(
                AppRoles.Viewer, AppPermissions.TaxInvoiceEdit));

            Assert.True(RolePermissions.Has(
                AppRoles.Viewer, AppPermissions.ProformaView));
        }

        /// <summary>
        /// An unknown or empty role must resolve to an empty set, not to
        /// a default-allow.
        /// </summary>
        [Theory]
        [InlineData("")]
        [InlineData("NotARole")]
        [InlineData("administrator")]   // case-sensitive by design
        public void Unknown_role_grants_nothing(string role)
        {
            Assert.False(RolePermissions.Has(role, AppPermissions.ProformaView));
            Assert.Empty(RolePermissions.For(role));
        }

        /// <summary>
        /// Legacy role names from earlier builds must keep working, or
        /// upgrading would strip existing users of their access.
        /// </summary>
        [Fact]
        public void Legacy_role_names_still_resolve()
        {
            Assert.True(RolePermissions.Has(
                AppRoles.Operator, AppPermissions.SheetStoreEdit));
            Assert.True(RolePermissions.Has(
                "Admin", AppPermissions.SettingsManage));
        }

        [Fact]
        public void Store_role_cannot_touch_invoices()
        {
            Assert.False(RolePermissions.Has(
                AppRoles.Store, AppPermissions.ProformaDelete));
            Assert.False(RolePermissions.Has(
                AppRoles.Store, AppPermissions.TaxInvoiceEdit));

            Assert.True(RolePermissions.Has(
                AppRoles.Store, AppPermissions.SheetStoreDelete));
        }
    }
}
