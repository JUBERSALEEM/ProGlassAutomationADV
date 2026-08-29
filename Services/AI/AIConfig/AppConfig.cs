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
        /// <summary>
        /// Base colour mode — "Light" (default) or "Dark". Restored at
        /// startup and persisted on every toggle (Settings UI and the
        /// AI-agent path). The visual theme family (VisualTheme) layers
        /// on top of this base.
        /// </summary>
        public string Theme { get; set; } = "Light";

        /// <summary>
        /// "Normal" | "Monet" | "Professional" | "Glass" | "Glassy"
        /// | "Ocean" — see VisualThemeEngine for what each does.
        /// </summary>
        public string VisualTheme { get; set; } = "Normal";

        /// <summary>
        /// Global font colour override (Settings → Appearance → Font
        /// Color). "" = Auto (the active theme/visual family owns the
        /// text palette); otherwise #RRGGBB — the VisualThemeEngine
        /// derives the Primary/Secondary/Muted hierarchy from it so
        /// text stays visible over transparent surfaces.
        /// </summary>
        public string FontColor { get; set; } = "";

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
        /// Accent preset key ("GlassBlue" | "RoyalBlue" | "Sky" | "Cyan"
        /// | "Teal" | "Emerald" | "Lime" | "Gold" | "Amber" | "Orange" |
        /// "Crimson" | "Rose" | "Fuchsia" | "Violet" | "Indigo" |
        /// "Slate" | "Black" | "Custom"). Re-applied on startup so the
        /// whole UI keeps the chosen colour between sessions.
        /// </summary>
        public string AccentPreset { get; set; } = "GlassBlue";

        /// <summary>
        /// Free-pick accent colour ("#RRGGBB"). Used when AccentPreset
        /// is "Custom"; ignored otherwise. Lets users choose any colour
        /// the palette chips do not offer — Monet and every derived
        /// brush adapt to it through ThemeManager.ApplyAccent.
        /// </summary>
        public string CustomAccentHex { get; set; } = "";

        /// <summary>
        /// Gradient surface support for the adaptive theme engine.
        /// When true (default) Monet glass surfaces, cards and the
        /// window background render as accent-derived gradients — a
        /// richer Material-You style depth. When false the same palette
        /// renders flat, which reads calmer on busy wallpapers.
        /// </summary>
        public bool UseGradientSurfaces { get; set; } = true;

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
    /// Per-field visibility flags for the Proforma Invoice wizard.
    ///
    /// ─── DESIGN CONTRACT ───────────────────────────────────────────
    /// • Every optional field in Steps 1 (Company), 2 (Client) and 3
    ///   (Project) has a boolean here. TRUE = show, FALSE = hide.
    /// • DB-required fields are NOT here — Company Name, Customer
    ///   Name and Proforma ID are always visible. Hiding them would
    ///   leave the operator unable to save a record.
    /// • Defaults are ALL TRUE so an existing install is byte-identical
    ///   to the previous release the first time it launches: the user
    ///   opts OUT of showing a field, they don't opt in.
    /// • The wizard code paths, validators, save serializers and DB
    ///   columns all keep working with the underlying properties;
    ///   hiding a field only stops it appearing in the UI. If the
    ///   field had a value before it was hidden, the value is
    ///   preserved on disk and reappears when the user re-enables it.
    /// • Bindings in the Step 1-3 XAML consume the singleton
    ///   ProGlassAutomation.Services.Features.ProformaFieldVisibility.
    ///   That helper mirrors these flags and raises PropertyChanged
    ///   on every save so the toggle animates fields in / out live
    ///   without an app restart.
    /// ────────────────────────────────────────────────────────────────
    /// </summary>
    public class ProformaInvoiceFeatureFlags
    {
        // ── Step 1: Company ────────────────────────────────────────
        public bool ShowCompanyTrn { get; set; } = true;
        public bool ShowCompanyLocation { get; set; } = true;
        public bool ShowRepresentative { get; set; } = true;
        public bool ShowRepresentativeMobile { get; set; } = true;
        public bool ShowCompanyEmail { get; set; } = true;
        public bool ShowBankName { get; set; } = true;
        public bool ShowAccountNumber { get; set; } = true;
        public bool ShowIssueDate { get; set; } = true;
        public bool ShowValidUntil { get; set; } = true;
        public bool ShowProformaStatus { get; set; } = true;

        // ── Step 2: Client ─────────────────────────────────────────
        public bool ShowCustomerTrn { get; set; } = true;
        public bool ShowCustomerReferenceCode { get; set; } = true;
        public bool ShowSalesRepresentative { get; set; } = true;
        public bool ShowDocumentPreparedBy { get; set; } = true;
        public bool ShowAttentionExecutive { get; set; } = true;
        public bool ShowClientContactPhone { get; set; } = true;
        public bool ShowBillingAddress { get; set; } = true;

        // ── Step 3: Project ────────────────────────────────────────
        public bool ShowProjectName { get; set; } = true;
        public bool ShowProjectReference { get; set; } = true;
        public bool ShowContractNo { get; set; } = true;
        public bool ShowLpoNumber { get; set; } = true;
        public bool ShowSiteDeliveryLocation { get; set; } = true;
    }

    /// <summary>
    /// Per-field visibility flags for the Inquiry editor.
    /// Same design contract as <see cref="ProformaInvoiceFeatureFlags"/>:
    ///   • Only OPTIONAL fields are here. DB-required
    ///     (CustomerName, ProjectName) never toggle.
    ///   • Defaults ALL TRUE → byte-identical upgrade.
    ///   • Hidden fields keep their DB value; re-enabling reveals it.
    /// The wizard binds via <see cref="Services.Features.InquiryFieldVisibility"/>.
    /// </summary>
    public class InquiryFeatureFlags
    {
        public bool ShowInquiryReference { get; set; } = true;
        public bool ShowCustomerLocation { get; set; } = true;
        public bool ShowProjectDescription { get; set; } = true;
        public bool ShowSalesman { get; set; } = true;
        public bool ShowPreparedBy { get; set; } = true;
        public bool ShowPriority { get; set; } = true;
        public bool ShowInquiryValue { get; set; } = true;
        public bool ShowNotes { get; set; } = true;
    }

    /// <summary>
    /// Per-field visibility flags for the Job Order production panel.
    /// Same design contract as <see cref="ProformaInvoiceFeatureFlags"/>:
    ///   • Only OPTIONAL fields are here. The DB-required / workflow
    ///     controls (Status, Priority, Delivery Date) never toggle —
    ///     hiding them would stop the job from being planned or saved.
    ///   • Defaults ALL TRUE → byte-identical upgrade.
    ///   • Hidden fields keep their stored value; re-enabling reveals it.
    /// The panel binds via <see cref="Services.Features.JobOrderFieldVisibility"/>.
    /// </summary>
    public class JobOrderFeatureFlags
    {
        public bool ShowAssignedTo { get; set; } = true;
        public bool ShowSiteDeliveryContact { get; set; } = true;
        public bool ShowSiteDeliveryPhone { get; set; } = true;
        public bool ShowSiteDeliveryAddress { get; set; } = true;
        public bool ShowDrawingReference { get; set; } = true;
        public bool ShowCuttingInstructions { get; set; } = true;
        public bool ShowInternalNotes { get; set; } = true;
    }

    /// <summary>
    /// Per-field visibility flags for the Delivery Note detail.
    /// Same design contract: only optional delivery-detail fields are
    /// toggleable; Status stays because it drives the dispatch workflow.
    /// </summary>
    public class DeliveryNoteFeatureFlags
    {
        public bool ShowDriverName { get; set; } = true;
        public bool ShowVehicleNumber { get; set; } = true;
        public bool ShowIsFoc { get; set; } = true;
        public bool ShowReceiverName { get; set; } = true;
        public bool ShowNotes { get; set; } = true;
    }

    /// <summary>
    /// Per-field visibility flags for the Tax Invoice detail.
    /// Same design contract: only optional accounting fields are
    /// toggleable; Issue Date stays because it anchors the invoice.
    /// </summary>
    public class TaxInvoiceFeatureFlags
    {
        public bool ShowDueDate { get; set; } = true;
        public bool ShowPaymentTerms { get; set; } = true;
        public bool ShowDiscountPercent { get; set; } = true;
        public bool ShowIban { get; set; } = true;
        public bool ShowSwiftCode { get; set; } = true;
    }

    /// <summary>
    /// Persisted preferences for the in-app screen recorder. Simple
    /// primitives only so the settings survive both a config-schema
    /// bump and a JSON round-trip. Kept as int rather than the
    /// ScreenRecordQuality enum so a value the enum no longer
    /// recognises (older config after an enum rename) hydrates as
    /// FullHd (1080) without crashing.
    ///
    /// UX contract (Aug 2026 senior audit):
    ///   • Quality, Fps, CaptureLiveAudio, CaptureMicrophone all
    ///     survive an app restart.
    ///   • Only an installed update that intentionally breaks the
    ///     schema resets them; ConfigService.Load's null-coalescing
    ///     hydration returns a fresh defaulted instance in that case.
    /// </summary>
    public class RecordingPreferences
    {
        /// <summary>Vertical resolution: 720, 1080, 1440 or 2160.</summary>
        public int QualityHeight { get; set; } = 1080;

        /// <summary>Target frame rate: 30, 40, 50 or 60.</summary>
        public int Fps { get; set; } = 30;

        /// <summary>Capture internal system audio via WASAPI loopback.</summary>
        public bool CaptureLiveAudio { get; set; } = true;

        /// <summary>Capture microphone via WASAPI.</summary>
        public bool CaptureMicrophone { get; set; } = true;

        /// <summary>Supersampled capture (higher-quality downscale).</summary>
        public bool Supersample { get; set; }

        /// <summary>
        /// Auto-dismiss the recording HUD after "SAVED" is shown.
        /// True by default so the user doesn't have to click Close
        /// on the overlay after every recording.
        /// </summary>
        public bool AutoCloseHudAfterSave { get; set; } = true;
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
        /// Per-field visibility flags for the Proforma Invoice
        /// wizard (Steps 1-3). Lets a non-coder operator turn off
        /// fields they never fill in without touching XAML. See
        /// <see cref="ProformaInvoiceFeatureFlags"/> for the
        /// design contract.
        /// </summary>
        public ProformaInvoiceFeatureFlags ProformaInvoiceFields { get; set; }
            = new ProformaInvoiceFeatureFlags();

        /// <summary>
        /// Per-field visibility flags for the Inquiry editor. Same
        /// design contract as <see cref="ProformaInvoiceFieldFlagsDoc"/>:
        /// defaults ALL true (byte-identical upgrade), only optional
        /// fields toggleable, hidden fields keep their stored value.
        /// </summary>
        public InquiryFeatureFlags InquiryFields { get; set; }
            = new InquiryFeatureFlags();

        /// <summary>
        /// Per-field visibility flags for the Job Order production panel.
        /// Mirrors <see cref="JobOrderFeatureFlags"/>; the panel binds via
        /// <see cref="Services.Features.JobOrderFieldVisibility"/>.
        /// </summary>
        public JobOrderFeatureFlags JobOrderFields { get; set; }
            = new JobOrderFeatureFlags();

        /// <summary>
        /// Per-field visibility flags for the Delivery Note detail.
        /// Mirrors <see cref="DeliveryNoteFeatureFlags"/>; the detail binds
        /// via <see cref="Services.Features.DeliveryNoteFieldVisibility"/>.
        /// </summary>
        public DeliveryNoteFeatureFlags DeliveryNoteFields { get; set; }
            = new DeliveryNoteFeatureFlags();

        /// <summary>
        /// Per-field visibility flags for the Tax Invoice detail.
        /// Mirrors <see cref="TaxInvoiceFeatureFlags"/>; the detail binds
        /// via <see cref="Services.Features.TaxInvoiceFieldVisibility"/>.
        /// </summary>
        public TaxInvoiceFeatureFlags TaxInvoiceFields { get; set; }
            = new TaxInvoiceFeatureFlags();

        /// <summary>
        /// Persisted user preferences for the in-app screen recorder
        /// (Demo Center card and top-bar recorder popup). Before this
        /// existed the user's last-picked quality / fps / mic / speaker
        /// choices were only kept in memory and reset every restart —
        /// a real usability complaint. Config bump is additive; older
        /// config.json files hydrate to defaults via ConfigService.Load's
        /// null-coalescing pass. Only reset on an installed update if
        /// the schema breaks in a future release.
        /// </summary>
        public RecordingPreferences Recording { get; set; }
            = new RecordingPreferences();

        /// <summary>
        /// Demo Center recording choices (quality / fps / audio) for the
        /// automated module recordings. Kept SEPARATE from Recording —
        /// the manual REC overlay and the demo generator have different
        /// jobs and must not overwrite each other.
        /// 2026-08-25: before this existed, DemoCenterViewModel reset to
        /// hard-coded defaults every time the Settings page rebuilt, and
        /// users saw their demo recording settings "reset themselves".
        /// Additive schema change — older config.json files hydrate to
        /// defaults via ConfigService.Load's null-coalescing pass.
        /// </summary>
        public RecordingPreferences DemoRecording { get; set; }
            = new RecordingPreferences();

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
        /// 2026-08-26 (cross-PC trial lockout): the machine
        /// fingerprint of the PC that first issued the auto-trial,
        /// recorded alongside <see cref="AutoTrialIssuedAtUtc"/>.
        ///
        /// WHY: this config file lives in %AppData% (Roaming), which
        /// OneDrive "known folder move" syncs BETWEEN a user's PCs.
        /// A bare boolean "trial was issued" therefore leaks across
        /// machines: PC_B inherits PC_A's flag and is refused the
        /// trial it is entitled to. The flag is now honoured only
        /// when the stored fingerprint matches the CURRENT machine
        /// (see TrialLedger.Read). Legacy configs without a
        /// fingerprint are treated as unattributable — the other
        /// three machine-local anchors (%ProgramData%, registry,
        /// DPAPI) keep the one-trial-per-machine rule on the same
        /// machine.
        /// </summary>
        public string AutoTrialFingerprint { get; set; } = "";

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
