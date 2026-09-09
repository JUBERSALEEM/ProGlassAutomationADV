// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Sharing/Providers/WhatsAppLauncher.cs
// PROJECT: ProGlassAutomation
// MODULE: Sharing Center — WhatsApp channel detection + launch (Stage 3)
//
// WHY THIS EXISTS (Sept 2026 senior audit — "WhatsApp never opens")
// ─────────────────────────────────────────────────────────────────
//   The provider used to open a bare https://wa.me/<n> link. On DESKTOP
//   that is NOT a chat — it is a browser interstitial ("Continue to
//   Chat") that most operators never click through, so from their seat
//   WhatsApp simply "did not open" and all they saw was the Explorer
//   folder reveal. This launcher fixes that by deep-linking straight into
//   whichever WhatsApp the machine actually has, on the recipient's chat,
//   with the message prefilled.
//
// CHANNEL ORDER (whichever is installed / logged in — the user's ask)
// ─────────────────────────────────────────────────────────────────
//   1. WhatsApp Desktop  — whatsapp://send?phone=<digits>&text=<msg>
//      Opens the installed, logged-in desktop client directly on the
//      chat. Detected via its registered URL protocol in the registry;
//      only attempted when that protocol is present.
//   2. WhatsApp Web      — https://web.whatsapp.com/send?phone=<digits>&text=<msg>
//      Opens the logged-in web session's chat in the default browser —
//      NO wa.me interstitial. This is the reliable fallback when the
//      desktop app is not installed.
//   3. wa.me             — https://wa.me/<digits>?text=<msg>
//      Last-resort universal link (older systems / unusual browsers).
//
// HONESTY. Every channel is still a HANDOFF: the message is prefilled but
// nothing is sent until the operator presses send, and WhatsApp links
// carry TEXT ONLY — the file is staged and revealed beside the chat for
// drag-in. The outcome is always ExternalAppOpened, never "Sent".
//
// Never throws: each launch attempt returns a Result; the provider walks
// the channels and reports the first success (or a combined failure).
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Common;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.Security;
using System;
using System.Runtime.InteropServices;

namespace ProGlassAutomation.Services.Sharing.Providers
{
    /// <summary>Which WhatsApp channel a launch attempt used.</summary>
    public enum WhatsAppChannel
    {
        Desktop,
        Web,
        WaMe
    }

    /// <summary>Result of a WhatsApp launch: the channel and the outcome.</summary>
    public sealed class WhatsAppLaunchResult
    {
        public bool IsSuccess { get; }
        public WhatsAppChannel Channel { get; }
        public string Error { get; }

        private WhatsAppLaunchResult(bool ok, WhatsAppChannel channel, string error)
        {
            IsSuccess = ok;
            Channel = channel;
            Error = error ?? "";
        }

        public static WhatsAppLaunchResult Ok(WhatsAppChannel channel) =>
            new(true, channel, "");

        public static WhatsAppLaunchResult Fail(string error) =>
            new(false, WhatsAppChannel.WaMe, error ?? "");

        /// <summary>Human label for the channel that opened.</summary>
        public string ChannelName => Channel switch
        {
            WhatsAppChannel.Desktop => "WhatsApp Desktop",
            WhatsAppChannel.Web => "WhatsApp Web",
            _ => "WhatsApp"
        };
    }

    /// <summary>
    /// Detects the available WhatsApp channel and opens the recipient's
    /// chat with a prefilled message. Pure launch logic — no staging, no
    /// audit (the provider owns those).
    /// </summary>
    public static class WhatsAppLauncher
    {
        /// <summary>
        /// Opens WhatsApp on <paramref name="digits"/> (E.164 digits, no
        /// '+') with <paramref name="message"/> prefilled, trying Desktop →
        /// Web → wa.me. Returns the channel that succeeded. Never throws.
        /// </summary>
        public static WhatsAppLaunchResult OpenChat(string digits, string message)
        {
            if (string.IsNullOrWhiteSpace(digits))
                return WhatsAppLaunchResult.Fail("No phone number to open.");

            string text = message ?? "";
            string encoded = Uri.EscapeDataString(text);
            string lastError = "";

            // 1. WhatsApp Desktop — only when its protocol is registered,
            // so we do not fire a whatsapp:// link into a "no app" error
            // dialog on machines without the desktop client.
            if (IsDesktopInstalled())
            {
                string deepLink = "whatsapp://send?phone=" + digits
                    + "&text=" + encoded;
                Result desktop = ProcessStartGuard.TryOpenUrl(deepLink);
                if (desktop.IsSuccess)
                    return WhatsAppLaunchResult.Ok(WhatsAppChannel.Desktop);
                lastError = desktop.Error;
                AppLogService.Debug(
                    "[Sharing.WhatsApp] desktop deep link failed, falling back to web: "
                    + desktop.Error);
            }

            // 2. WhatsApp Web — opens the logged-in web chat directly
            // (no wa.me interstitial).
            string webUrl = "https://web.whatsapp.com/send?phone=" + digits
                + "&text=" + encoded;
            Result web = ProcessStartGuard.TryOpenUrl(webUrl);
            if (web.IsSuccess)
                return WhatsAppLaunchResult.Ok(WhatsAppChannel.Web);
            lastError = web.Error.Length > 0 ? web.Error : lastError;
            AppLogService.Debug(
                "[Sharing.WhatsApp] web link failed, falling back to wa.me: "
                + web.Error);

            // 3. wa.me — universal last resort.
            string waMe = "https://wa.me/" + digits + "?text=" + encoded;
            Result universal = ProcessStartGuard.TryOpenUrl(waMe);
            if (universal.IsSuccess)
                return WhatsAppLaunchResult.Ok(WhatsAppChannel.WaMe);

            return WhatsAppLaunchResult.Fail(
                universal.Error.Length > 0 ? universal.Error : lastError);
        }

        /// <summary>
        /// True when WhatsApp Desktop has registered its "whatsapp:" URL
        /// protocol handler (Store or classic install). Checks both the
        /// per-user and machine class roots. Never throws; false on any
        /// non-Windows / access issue so the caller falls back to web.
        /// </summary>
        public static bool IsDesktopInstalled()
        {
            try
            {
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    return false;
                return ProtocolRegistered();
            }
            catch (Exception ex)
            {
                AppLogService.Debug(
                    "[Sharing.WhatsApp] desktop detection failed: " + ex.Message);
                return false;
            }
        }

        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        private static bool ProtocolRegistered()
        {
            // HKCU\Software\Classes\whatsapp (per-user install / Store app)
            // then HKCR\whatsapp (machine-wide classic install). A URL
            // protocol key carries a "URL Protocol" value and a
            // shell\open\command default — presence of the key with a
            // command is enough to attempt the deep link.
            if (HasOpenCommand(
                    Microsoft.Win32.Registry.CurrentUser,
                    @"Software\Classes\whatsapp"))
                return true;

            if (HasOpenCommand(
                    Microsoft.Win32.Registry.ClassesRoot,
                    "whatsapp"))
                return true;

            return false;
        }

        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        private static bool HasOpenCommand(
            Microsoft.Win32.RegistryKey baseKey, string subPath)
        {
            try
            {
                using var protocol = baseKey.OpenSubKey(subPath, writable: false);
                if (protocol == null) return false;

                // Must actually declare itself a URL protocol.
                if (protocol.GetValue("URL Protocol") == null) return false;

                using var command = protocol.OpenSubKey(
                    @"shell\open\command", writable: false);
                string? cmd = command?.GetValue(null) as string;
                return !string.IsNullOrWhiteSpace(cmd);
            }
            catch (Exception ex)
            {
                AppLogService.Debug(
                    $"[Sharing.WhatsApp] registry probe '{subPath}' failed: {ex.Message}");
                return false;
            }
        }
    }
}
