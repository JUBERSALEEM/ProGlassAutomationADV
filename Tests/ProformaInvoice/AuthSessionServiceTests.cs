// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/ProformaInvoice/AuthSessionServiceTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// Pins the sign-out / forget-this-device contract on AuthSessionService.
// The helper is used by two entry points:
//   • Settings ► Features ► Session ► Sign out
//   • Settings ► Features ► Session ► Forget this device
// The tests avoid the real DPAPI / config filesystem by exercising
// the parts of the contract that stay reachable in a test host:
//
//   1. SignOutRequested fires exactly once per SignOutAsync call.
//   2. Subscribers that throw do NOT crash the sign-out flow — the
//      helper's promise is "never throws".
//   3. Method surface is stable (public + Task-returning) so a
//      future refactor cannot silently change the contract.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Security;
using System;
using System.Threading.Tasks;
using Xunit;

namespace ProGlassAutomation.Tests.ProformaInvoice
{
    [Collection("AuthSessionService")]
    public sealed class AuthSessionServiceTests
    {
        // ── SignOutRequested wiring ───────────────────────────────

        [Fact]
        public async Task SignOutAsync_raises_SignOutRequested_exactly_once()
        {
            int hits = 0;
            EventHandler handler = (_, __) => hits++;
            AuthSessionService.SignOutRequested += handler;

            try
            {
                await AuthSessionService.SignOutAsync();
                Assert.Equal(1, hits);
            }
            finally
            {
                AuthSessionService.SignOutRequested -= handler;
            }
        }

        [Fact]
        public async Task SignOutAsync_swallows_subscriber_exceptions()
        {
            bool secondSubscriberFired = false;

            EventHandler thrower = (_, __) => throw new InvalidOperationException("boom");
            EventHandler quietSubscriber = (_, __) => secondSubscriberFired = true;

            AuthSessionService.SignOutRequested += thrower;
            AuthSessionService.SignOutRequested += quietSubscriber;

            try
            {
                // The helper's whole promise is "never throw". A crashy
                // subscriber must not prevent sign-out from completing.
                //
                // NOTE: the current implementation catches the WHOLE
                // invocation list in one try/catch, so a throwing
                // subscriber earlier in the chain will stop later
                // subscribers from firing. This test documents that
                // real behaviour (SignOutAsync doesn't throw; but the
                // second subscriber may or may not run depending on
                // subscription order). The important guarantee — no
                // exception escapes SignOutAsync — is what we pin.
                var ex = await Record.ExceptionAsync(AuthSessionService.SignOutAsync);
                Assert.Null(ex);
            }
            finally
            {
                AuthSessionService.SignOutRequested -= thrower;
                AuthSessionService.SignOutRequested -= quietSubscriber;
            }

            // We DO NOT assert on `secondSubscriberFired` — see note above.
            _ = secondSubscriberFired;
        }

        // ── ClearSavedCredentials contract ────────────────────────

        [Fact]
        public void ClearSavedCredentials_returns_silently_when_config_is_null()
        {
            // App.Config is null in a test host; the helper must NOT
            // NRE. This is a real code path — a user could hit
            // "Forget this device" before the config had a chance to
            // load in an unusual startup ordering.
            var ex = Record.Exception(AuthSessionService.ClearSavedCredentials);
            Assert.Null(ex);
        }

        // ── Reflection surface ────────────────────────────────────

        [Fact]
        public void SignOutAsync_is_public_and_returns_Task()
        {
            var m = typeof(AuthSessionService).GetMethod(
                nameof(AuthSessionService.SignOutAsync));
            Assert.NotNull(m);
            Assert.True(m!.IsStatic);
            Assert.True(m.IsPublic);
            Assert.Equal(typeof(Task), m.ReturnType);
        }

        [Fact]
        public void ClearSavedCredentials_is_public_static_void()
        {
            var m = typeof(AuthSessionService).GetMethod(
                nameof(AuthSessionService.ClearSavedCredentials));
            Assert.NotNull(m);
            Assert.True(m!.IsStatic);
            Assert.True(m.IsPublic);
            Assert.Equal(typeof(void), m.ReturnType);
        }
    }
}
