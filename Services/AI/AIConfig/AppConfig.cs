using ProGlassAutomation.Services.AI.AIService;
using System.Collections.Generic;

namespace ProGlassAutomation.Services.AI.AIConfig
{
    /// <summary>Wallpaper configuration for one module (§9).</summary>
    public class ModuleWallpaperSettings
    {
        public bool Enabled { get; set; }
        public string Path { get; set; } = "";
        public double Opacity { get; set; } = 0.85;
    }

    /// <summary>
    /// Appearance preferences (v2 visual theme engine + wallpaper).
    /// No secrets here — plain settings only.
    /// </summary>
    public class AppearanceSettings
    {
        /// <summary>"Normal" | "Monet" | "Professional".</summary>
        public string VisualTheme { get; set; } = "Normal";

        /// <summary>
        /// When true, rounded pills and cards draw a strong near-black
        /// definition edge so they stay visually separate from tinted
        /// (Monet) surfaces instead of blending into them.
        /// </summary>
        public bool UseDefinedOutline { get; set; }

        /// <summary>
        /// Uniform border thickness (in DIPs) for every card / panel in the
        /// app. Defaults to 1.5. Applied globally through the
        /// CardBorderThickness theme resource so one setting controls all
        /// cards instead of editing them individually.
        /// </summary>
        public double CardBorderThickness { get; set; } = 1.5;

        /// <summary>
        /// Uniform corner radius (DIPs) for every card / panel. Defaults
        /// to 12 — the tighter industrial look. Applied globally through
        /// CardCornerRadius so one slider restyles the whole app.
        /// ControlCornerRadius and CardCornerRadiusLarge are derived from
        /// this value (radius − 2 and radius + 4).
        /// </summary>
        public double CardCornerRadius { get; set; } = 12;

        /// <summary>
        /// Display density: "Compact" | "Normal" | "Comfortable".
        /// One setting shrinks inputs, buttons, titles and card padding
        /// app-wide so laptop screens can see the data grid.
        /// Default Compact — factory laptops are typically 1366×768.
        /// </summary>
        public string DisplayDensity { get; set; } = "Compact";

        /// <summary>
        /// User UI zoom 0.80–1.25. 1.0 is native layout. Ctrl+Wheel.
        /// </summary>
        public double UiScale { get; set; } = 1.0;

        /// <summary>Global wallpaper image path (empty = none).</summary>
        public string WallpaperPath { get; set; } = "";

        /// <summary>How strongly the wallpaper shows through (0.3–1).</summary>
        public double WallpaperOpacity { get; set; } = 0.85;

        /// <summary>
        /// Per-module wallpapers, keyed by WallpaperModules constants
        /// (e.g. "ProformaInvoice"). Missing entries = no wallpaper.
        /// </summary>
        public Dictionary<string, ModuleWallpaperSettings> ModuleWallpapers
        { get; set; } = new();

        /// <summary>
        /// Accent preset key ("GlassBlue" | "Emerald" | "Amber" |
        /// "Rose" | "Violet" | "Teal"). Re-applied on startup so the
        /// whole UI keeps the chosen colour between sessions.
        /// </summary>
        public string AccentPreset { get; set; } = "GlassBlue";

        /// <summary>
        /// Print layout id: ClassicNavy | ModernTeal | EditorialInk |
        /// IndustrialSteel | MinimalIvory. Applied to every PDF.
        /// </summary>
        public string PdfLayoutId { get; set; } = "ClassicNavy";

        /// <summary>
        /// Global UI font family (applied through the AppFontFamily resource).
        /// "Segoe UI" by default. May be a system font or an imported one
        /// (see FontService). Empty/unknown names fall back to Segoe UI.
        /// </summary>
        public string FontFamilyName { get; set; } = "Segoe UI";

        /// <summary>
        /// Global UI font weight: "Normal" | "Bold".
        /// </summary>
        public string FontWeightName { get; set; } = "Normal";

        /// <summary>
        /// Global UI font style: "Normal" | "Italic".
        /// </summary>
        public string FontStyleName { get; set; } = "Normal";
    }

    /// <summary>
    /// Root config container persisted to disk as JSON.
    ///
    /// SECURITY (v2): API keys never reach the JSON file. They live in
    /// the DPAPI-encrypted SecretVault and are hydrated into AiSettings
    /// in memory by ConfigService. With RememberApiKeys=false keys exist
    /// only for the running session.
    /// </summary>
    public class AppConfig
    {
        public AiSettings Ai { get; set; } = new AiSettings();
        public bool OnlineFeaturesEnabled { get; set; } = true;
        public AppearanceSettings Appearance { get; set; } = new();

        /// <summary>
        /// Last signed-in username. Never a password. Restored on the
        /// login screen so the operator does not retype it every launch.
        /// </summary>
        public string LastUsername { get; set; } = "";

        /// <summary>When false the last username is cleared on sign-in.</summary>
        public bool RememberUsername { get; set; } = true;

        // ── Skip-login until next update (Auth-2 pill toggle) ──────────
        //
        // Behavioural contract (mirrors iOS "Face ID / stay signed in"
        // toggles): while this flag is on AND the current app version
        // still equals SkipLoginBoundVersion, the splash bypasses the
        // sign-in form entirely and auto-signs in with the saved
        // credentials. As soon as the app updates (AppInfo.Version
        // changes) the bound version no longer matches, so the next
        // launch shows the login form again — the user re-confirms
        // their credentials on the new build, at which point the pill
        // can be re-armed for the new version.
        //
        // The password itself is NEVER stored in this config file. It
        // lives in SecretVault (DPAPI, CurrentUser scope) under the
        // key SecretKeys.AutoLoginPassword. Deleting the vault or
        // signing in as a different user clears it automatically.

        /// <summary>
        /// When true, the splash skips the sign-in form on next launch
        /// as long as <see cref="SkipLoginBoundVersion"/> still equals
        /// the running app version. Cleared automatically on sign-out
        /// or when the user flips the pill off.
        /// </summary>
        public bool SkipLoginUntilUpdate { get; set; } = false;

        /// <summary>
        /// The app version (<c>AppInfo.Version</c>) that armed the
        /// skip-login pill. When the app updates to a newer version
        /// the pill deliberately stops working so the user re-enters
        /// their password on the new build. Empty when the pill has
        /// never been armed.
        /// </summary>
        public string SkipLoginBoundVersion { get; set; } = "";

        // ── Auto-trial bookkeeping (commit 2 of the licensing refactor) ──
        //
        // The shipped app issues a 7-day trial on first launch when
        // there is no license file on disk. To make that exactly
        // one-per-machine and not re-issued on every launch, we
        // remember that a trial has been issued in the persisted
        // config. The flag is set on the same launch the trial
        // .lic file is written, and cleared by
        // LicenseService.EnforceTrialExpiry when the trial has
        // expired (so a customer who activates a real license
        // after the trial window is not penalised).

        /// <summary>
        /// UTC timestamp of the first auto-issued trial on
        /// this machine, or null if no trial has been
        /// issued yet. Used as the one-per-machine
        /// gate by <see cref="Services.Licensing.LicenseService"/>.
        /// </summary>
        public DateTime? AutoTrialIssuedAtUtc { get; set; }

        /// <summary>
        /// Convenience mirror of <see cref="AutoTrialIssuedAtUtc"/>:
        /// true iff a trial was ever issued on this machine.
        /// Kept as a separate flag so config code can read
        /// the boolean without null-checks. Both fields are
        /// kept in sync by the licence code.
        /// </summary>
        public bool AutoTrialWasIssued
        {
            get => AutoTrialIssuedAtUtc.HasValue;
            set
            {
                if (value)
                {
                    // Record the moment the trial was first
                    // issued. Preserve the original timestamp
                    // if one is already present (do not
                    // overwrite on every save).
                    AutoTrialIssuedAtUtc ??= DateTime.UtcNow;
                }
                else
                {
                    // Explicit clear — used by
                    // LicenseService.EnforceTrialExpiry when
                    // the trial window has passed and a real
                    // license has not been activated, so a
                    // future launch does not re-issue.
                    AutoTrialIssuedAtUtc = null;
                }
            }
        }
    }
}
