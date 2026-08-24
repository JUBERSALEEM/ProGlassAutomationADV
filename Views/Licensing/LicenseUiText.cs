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
            // Auto-issued 7-day trial that has expired. The
            // license file is gone; the user must activate a
            // real license to continue. Wording deliberately
            // matches "License expired" so the message in
            // the UI is unambiguous.
            LicenseStatus.TrialExpired => "Trial expired",
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
                "No license file was found in the data folder. " +
                "Activate a license or start the 7-day trial.",
            LicenseStatus.Invalid =>
                "The license file is present but its signature " +
                "could not be verified. The file may be corrupt, " +
                "signed with a different key, or for an older " +
                "app version. Re-issue the license from the " +
                "vendor.",
            LicenseStatus.GracePeriod =>
                "The license is past its expiry date but the " +
                "grace period is still active. The app runs in " +
                "read-only mode until the grace period ends. " +
                "Renew the license to restore full access.",
            LicenseStatus.Expired =>
                "The license is expired and the grace period has " +
                "elapsed. The app cannot run until a valid " +
                "license is activated.",
            LicenseStatus.ExpiringSoon =>
                "The license is valid but expires within 14 days. " +
                "Contact your vendor to renew before it expires.",
            LicenseStatus.Valid =>
                "The license is valid and all licensed features " +
                "are available.",
            LicenseStatus.Trial =>
                "A 7-day trial is active. The trial expires " +
                "automatically; activate a full license before " +
                "then to avoid interruption.",
            LicenseStatus.TrialExpired =>
                "The 7-day trial has expired and the trial " +
                "license has been removed from this machine. " +
                "Activate a full license to continue using " +
                "ProGlass Automation.",
            LicenseStatus.MachineMismatch =>
                "This license is bound to a different machine. " +
                "Ask the vendor to re-issue the license for this " +
                "machine's fingerprint (Settings → About).",
            LicenseStatus.ActivationLimit =>
                "This license has been activated on the maximum " +
                "number of devices. Deactivate another device " +
                "or contact your vendor to increase the limit.",
            _ => ""
        };
    }
}
