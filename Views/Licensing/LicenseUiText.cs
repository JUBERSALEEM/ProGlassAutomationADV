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
                "No signed offline activation response is currently active on this machine. " +
                "Generate an activation request in Settings → License, send it to your vendor, and import the signed response they return.",
            LicenseStatus.Invalid =>
                "The installed activation state could not be trusted. The response may be corrupt, bound to a different request, signed with an unknown key, or from the legacy insecure format. Generate a fresh request and ask your vendor to re-issue it.",
            LicenseStatus.GracePeriod =>
                "The signed activation response is past its main expiry date but still inside its signed offline grace window. The app runs in read-only mode until the grace period ends. Renew the activation response to restore full access.",
            LicenseStatus.Expired =>
                "The signed activation response is expired and its grace period has elapsed. Generate a fresh request and obtain a renewed response from your vendor.",
            LicenseStatus.ExpiringSoon =>
                "The signed activation response is valid but expires within 14 days. Contact your vendor to renew before it expires.",
            LicenseStatus.Valid =>
                "The signed offline activation response is valid and the licensed features are available.",
            LicenseStatus.Trial =>
                "A vendor-issued offline trial response is active. Renew it or replace it with a full signed activation response before the trial window ends.",
            LicenseStatus.TrialExpired =>
                "The signed offline trial response has expired. All your data is preserved, but the app is now in read-only mode until you import a renewed signed response or a full activation.",
            LicenseStatus.MachineMismatch =>
                "This activation response is bound to a different machine fingerprint. Generate a fresh activation request from this PC and ask the vendor to re-issue it.",
            LicenseStatus.ActivationLimit =>
                "The legacy activation-limit state was reached. Re-issue the license under the new offline activation flow.",
            _ => ""
        };
    }
}
