// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Sharing/WhatsAppLauncherTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// Pins the WhatsApp channel launcher's SAFE surface only:
//   • IsDesktopInstalled() never throws and returns a bool (registry
//     probe is exception-guarded).
//   • WhatsAppLaunchResult factory + channel naming.
//   • Empty phone is rejected without touching Process.Start.
//
// OpenChat() with a real number is NOT exercised here — it launches an
// external browser/app, which a unit test must never do (same rule as
// ProviderValidationTests: Validate only, never ShareAsync).
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Sharing.Providers;
using Xunit;

namespace ProGlassAutomation.Tests.Sharing
{
    public class WhatsAppLauncherTests
    {
        [Fact]
        public void IsDesktopInstalled_never_throws_and_returns_bool()
        {
            // Whatever the CI machine has (or hasn't), this must be a safe,
            // non-throwing probe — the provider relies on it degrading to
            // the web channel rather than crashing the share. It must also
            // be STABLE (multi-signal detection must not flip between
            // calls) so the launch order is deterministic.
            bool first = WhatsAppLauncher.IsDesktopInstalled();
            bool second = WhatsAppLauncher.IsDesktopInstalled();
            Assert.Equal(first, second);
        }

        [Fact]
        public void OpenChat_rejects_empty_phone_without_launching()
        {
            var result = WhatsAppLauncher.OpenChat("", "hello");
            Assert.False(result.IsSuccess);
            Assert.NotEqual("", result.Error);
        }

        [Fact]
        public void LaunchResult_channel_names_are_human_readable()
        {
            Assert.Equal("WhatsApp Desktop",
                WhatsAppLaunchResult.Ok(WhatsAppChannel.Desktop).ChannelName);
            Assert.Equal("WhatsApp Web",
                WhatsAppLaunchResult.Ok(WhatsAppChannel.Web).ChannelName);
            Assert.Equal("WhatsApp",
                WhatsAppLaunchResult.Ok(WhatsAppChannel.WaMe).ChannelName);
        }

        [Fact]
        public void LaunchResult_success_and_failure_flags()
        {
            Assert.True(WhatsAppLaunchResult.Ok(WhatsAppChannel.Web).IsSuccess);

            var fail = WhatsAppLaunchResult.Fail("nope");
            Assert.False(fail.IsSuccess);
            Assert.Equal("nope", fail.Error);
        }
    }
}
