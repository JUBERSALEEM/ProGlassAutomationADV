// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/Licensing/LicenseUiText.cs
// PROJECT: ProGlassAutomation
// MODULE: Commercial licensing — UI text helpers
//
// Small static helper that turns a <see cref="LicenseStatus"/>
// into a one-line badge + a longer human description. Used by
// the welcome view, the About page, and any future "license
// required" banners. Centralised so the wording is consistent
// across the app.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Licensing;

namespace ProGlassAutomation.Views.Licensing
{
    /// <summary>
    /// UI string helpers for the licensing surface.
    /// </summary>
    public static class LicenseUiText
    {
        /// <summary>
        /// Short, one-line badge text. Suitable for a
        /// status pill or ribbon.
        /// </summary>
        public static string StatusBadge(LicenseStatus status) => status switch
        {
            LicenseStatus.NotActivated => "Not activated",
            LicenseStatus.Invalid => "License invalid",
            LicenseStatus.GracePeriod => "Expired (read-only)",
            LicenseStatus.Expired => "License expired",
            LicenseStatus.ExpiringSoon => "Expiring soon",
            LicenseStatus.Valid => "Activated",
            LicenseStatus.Trial => "Trial",
            LicenseStatus.MachineMismatch => "Wrong machine",
            LicenseStatus.ActivationLimit => "Activation limit reached",
            // Trial response that has ended. The app remains
            // in read-only mode until the user imports a new
            // signed response. The badge is deliberately
            // explicit so it stands out from the paid
            // "License expired" state.
            LicenseStatus.TrialExpired => "Trial ended · read-only",
            _ => status.ToString()
        };

        /// <summary>
        /// Long-form description for the about page or
        /// first-run messaging. Includes what the user
        /// should do next.
        /// </summary>
        public static string StatusDescription(LicenseStatus status) => status switch
        {
            LicenseStatus.NotActivated =>
                "No license is active on this machine yet. Paste the license key your vendor sent you in Settings → License, or use the advanced activation request flow.",
            LicenseStatus.Invalid =>
                "The installed license state could not be trusted. The key or response may be corrupt, signed with an unknown key, or from the legacy insecure format. Paste a fresh vendor-signed license key and try again.",
            LicenseStatus.GracePeriod =>
                "The license is past its main expiry date but still inside its signed grace window. The app runs in read-only mode until the grace period ends. Install a renewed license key to restore full access.",
            LicenseStatus.Expired =>
                "The license is expired and its grace period has elapsed. Install a renewed license key from your vendor to restore full access.",
            LicenseStatus.ExpiringSoon =>
                "The license is valid but expires within 14 days. Contact your vendor to renew before it expires.",
            LicenseStatus.Valid =>
                "A vendor-signed license is active and the licensed features are available.",
            LicenseStatus.Trial =>
                "A trial is active on this machine. Install a vendor-signed license key before the trial window ends to keep working without interruption.",
            LicenseStatus.TrialExpired =>
                "The trial has ended. All your data is preserved, but the app is now in read-only mode until you install a vendor-signed license key.",
            LicenseStatus.MachineMismatch =>
                "This license is bound to a different machine fingerprint. Ask your vendor for a key bound to this PC's ID, or generate a fresh activation request from this PC.",
            LicenseStatus.ActivationLimit =>
                "The legacy activation-limit state was reached. Re-issue the license as a signed license key.",
            _ => ""
        };
    }
}
