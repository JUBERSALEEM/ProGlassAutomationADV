using ProGlassAutomation.Services.AI.AIService;
using ProGlassAutomation.Services.Theme;
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

    /// <summary>Professional local/LAN database connectivity settings.</summary>
    public class DataConnectivitySettings
    {
        /// <summary>Use a shared LAN/Wi-Fi folder for all business SQLite databases.</summary>
        public bool UseSharedDataRoot { get; set; }

        /// <summary>UNC or mapped-drive folder, e.g. \\SERVER\ProGlassData.</summary>
        public string SharedDataRootPath { get; set; } = "";

        /// <summary>When true, copy the current local data set into an empty shared folder before switching.</summary>
        public bool CopyLocalDataOnEnable { get; set; } = true;

        /// <summary>Station label for audit/support; defaults to machine name when blank.</summary>
        public string WorkstationName { get; set; } = "";
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
        /// UI experience family (2026-09-18, GB-P14): "Professional"
        /// (default) or "Personal" (the Glass Blush second experience).
        /// Additive config — older config.json files hydrate to the
        /// "Professional" default via ConfigService.Load's null-coalescing
        /// pass. The MainViewModel reads this once at startup and persists
        /// it on every switch so the choice survives a restart without
        /// relaunching the app.
        /// </summary>
        public string UiExperience { get; set; } = "Professional";

        /// <summary>
        /// "Normal (Industrial)" (displayed "Industrial") | "Monet" |
        /// "Crystal Fluid" (displayed "iPhone 17 Pro Max") | "iOS 26" —
        /// see VisualThemeEngine for what each does. 2026-09-13
        /// industrial reform (owner ask: "strictness industrial
        /// pattern"): the four selectable families are Industrial (the
        /// strict default), Monet, iPhone 17 Pro Max and iOS 26. The
        /// cream SaaS "Normal" value and every unknown legacy value
        /// migrate to Normal (Industrial) via the engine's Normalize
        /// mapping.
        /// The eight selectable visual families are defined by VisualThemeEngine.
        /// Default: Liquid Glass. ConfigService migrates older family identities.
        /// </summary>
        public string VisualTheme { get; set; } = "Liquid Glass";

        /// <summary>
        /// One-time migration marker (2026-09-13): set the first time a
        /// saved <see cref="VisualTheme"/> of "Normal" is folded to the
        /// strict industrial default "Normal (Industrial)". Keeps the
        /// migration from re-firing if the user later deliberately
        /// re-picks a family.
        /// </summary>
        public bool VisualThemeMigrated { get; set; }

        /// <summary>One-time migration to the eight selectable theme families.</summary>
        public bool EightThemeMigrationApplied { get; set; }

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
        /// Custom card-edge colour (Settings → Appearance → Define Card
        /// Edges). Empty = follow the theme's strong outline colour
        /// (OutlineStrongBrush); otherwise a #RGB/#RRGGBB or named colour
        /// applied to every card border app-wide through the GlassBorder
        /// surface tokens. Parsed by AccentPresets.TryParseHex.
        /// </summary>
        public string CardEdgeColor { get; set; } = "";

        /// <summary>
        /// Uniform border thickness (in DIPs) for every card / panel in the
        /// app. Defaults to 1.5. Applied globally through the
        /// CardBorderThickness theme resource so one setting controls all
        /// cards instead of editing them individually.
        /// </summary>
        public double CardBorderThickness { get; set; } = 1.5;

        /// <summary>
        /// Card EDGE weight (in DIPs, 1–100) — the defined corner-border
        /// line around cards (Settings → Appearance → Define Card Edges).
        /// Consumed by the card surface styles through the CardEdgeThickness
        /// token, separate from <see cref="CardBorderThickness"/> (the
        /// subtle input/chrome hairline) so a bold industrial edge never
        /// inflates text boxes and table gridlines. Default 2.
        /// </summary>
        public double CardEdgeThickness { get; set; } = 2.0;

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

        /// <summary>CRYSTAL FLUID (2026-09-05, spec §43) — reduced motion:
        /// true = the water/surface/caustic/bubble choreography and card
        /// tilt render STATIC everywhere; functional transitions stay.
        /// Additive + default false = migration-safe for existing saves.</summary>
        public bool ReducedMotion { get; set; } = false;

        /// <summary>
        /// Wallpaper mode (2026-09-05 — Wallpaper Studio).
        ///   "Auto"        (DEFAULT, migration-safe): a saved WallpaperPath
        ///                 keeps winning (legacy behavior); with no path the
        ///                 theme-linked built-in scene shows instead.
        ///   "ThemeLinked" the dynamic code-drawn scene ALWAYS follows the
        ///                 active visual family/accent (user opt-in).
        ///   "File"        the image at WallpaperPath (downloaded or browsed).
        ///   "Scene"       one pinned built-in scene (BuiltinScene below).
        /// </summary>
        public string WallpaperMode { get; set; } = "Auto";

        /// <summary>Pinned built-in scene key (a VisualThemeEngine family
        /// name from BuiltinWallpaperScenes.All) when WallpaperMode =
        /// "Scene". Empty = follow the active family.</summary>
        public string BuiltinScene { get; set; } = "";

        /// <summary>OS window backdrop (2026-09-05, EXPERIMENTAL):
        /// "Off" (default — the painted liquid-glass stage), "Mica" or
        /// "Acrylic" (Windows 11 22H2+ DWM-rendered blur behind the app,
        /// see SystemBackdropService).</summary>
        public string WindowBackdrop { get; set; } = "Off";

        /// <summary>
        /// Splash Screen Style / Theme variant:
        /// 0 = "Auto (Cycle on Boot)"
        /// 1 = "Cinematic Ambient Mesh"
        /// 2 = "Crystal Glassmorphism"
        /// 3 = "Executive Minimalist"
        /// 4 = "Industrial CAD Blueprint"
        /// 5 = "iOS26 Liquid Stage"
        /// 6 = "Cyber Industrial Monolith"
        /// 7 = "Nordic Frosted Slate"
        /// 8 = "Dynamic Aurora Gradient"
        /// 9 = "Glass Prism Optical Bevel" (Default Approved)
        /// 10 = "Minimalist Indigo Monogram"
        /// </summary>
        public int SplashStyle { get; set; } = 9;

        /// <summary>
        /// Track the last boot splash index to cycle automatically.
        /// </summary>
        public int LastBootSplashIndex { get; set; } = 1;

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
        /// Semantic ERP surface material mode. "Glass" keeps the current
        /// frosted/translucent family identity; "Solid" calms surfaces for
        /// high-density production use; "Transparent" lets wallpaper/scene
        /// layers show through more strongly. Runtime-only theme tokens are
        /// derived from this and the per-level opacity/border/radius values.
        /// </summary>
        public string SurfaceMaterialMode { get; set; } = "Glass";

        /// <summary>
        /// Module-skin frost blur 1–100 (Settings → Appearance).
        /// Applied as BlurEffect on ModuleWallpaperBrush only — never on
        /// card children (that would blur text). 28 = professional default.
        /// </summary>
        public double CardBlurIntensity { get; set; } = 28;

        /// <summary>
        /// Frost tint for semantic cards (#RRGGBB). Empty = theme surface.
        /// Painted as itself at CardFrostStrength 100. Not mixed with the
        /// page grey, which is why a picked black used to read as grey.
        /// </summary>
        public string CardFrostTint { get; set; } = "";

        /// <summary>
        /// How much of the picked plate is used. 0 = white, 100 = the
        /// exact swatch. Default 100 so a pick matches the card.
        /// </summary>
        public double CardFrostStrength { get; set; } = 100;

        /// <summary>
        /// Alpha of the painted plate. 0 = clear, 100 = opaque. Default
        /// 100 so the colour is not composited back toward white.
        /// </summary>
        public double CardFrostOpacity { get; set; } = 100;

        /// <summary>Opacity of the major module/page surface level.</summary>
        public double SurfacePrimaryOpacity { get; set; } = 0.82;

        /// <summary>Opacity of nested group/filter/KPI surface level.</summary>
        public double SurfaceSecondaryOpacity { get; set; } = 0.94;

        /// <summary>Opacity of compact chip/badge/detail surface level.</summary>
        public double SurfaceDetailOpacity { get; set; } = 0.86;

        /// <summary>Optional Primary radius override. Null follows CardCornerRadius.</summary>
        public double? SurfacePrimaryRadius { get; set; }

        /// <summary>Optional Secondary radius override. Null follows CardCornerRadius.</summary>
        public double? SurfaceSecondaryRadius { get; set; }

        /// <summary>Optional Detail radius override. Null follows ControlCornerRadius.</summary>
        public double? SurfaceDetailRadius { get; set; }

        /// <summary>Optional Primary border override. Null follows CardEdgeThickness.</summary>
        public double? SurfacePrimaryBorderThickness { get; set; }

        /// <summary>Optional Secondary border override. Null follows CardBorderThickness.</summary>
        public double? SurfaceSecondaryBorderThickness { get; set; }

        /// <summary>Optional Detail border override. Null follows a 1 DIP hairline.</summary>
        public double? SurfaceDetailBorderThickness { get; set; }

        /// <summary>
        /// False makes all semantic ERP surfaces square while preserving the
        /// saved radius numbers, useful for strict industrial workstation UI.
        /// </summary>
        public bool UseRoundedSurfaces { get; set; } = true;

        /// <summary>
        /// Enables lightweight elevation only for semantic primary surfaces.
        /// Kept off by default to preserve performance on older WPF/GPU hosts.
        /// </summary>
        public bool UsePrimarySurfaceElevation { get; set; } = false;

        /// <summary>
        /// Primary overlay opacity for glass/wallpaper families. 0 = disabled.
        /// The runtime token is separate from business content and print output.
        /// </summary>
        public double PrimarySurfaceOverlayOpacity { get; set; } = 0.0;

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

        /// <summary>
        /// Global UI font text decoration: true for underline.
        /// </summary>
        public bool FontUnderline { get; set; } = false;

        /// <summary>
        /// Theme customization (Settings → Themes, 2026-09-06).
        /// Family-aware: only the Crystal Fluid (Ios-17pm) material axes
        /// are persisted here (they map 1:1 onto the Crystal*
        /// DynamicResource tokens the engine paints). Normal/Monet keep
        /// their existing Appearance controls (CardCornerRadius,
        /// CardBorderThickness, UseGradientSurfaces) — no parallel state.
        /// Defaults equal CrystalFluidSettings.Baseline* so a fresh
        /// install renders exactly like the uncustomized design, and the
        /// property is additive (missing in old saves → defaults).
        /// </summary>
        public ThemeCustomizationSettings ThemeCustomization { get; set; } = new();
    }

    /// <summary>
    /// Persisted Crystal Fluid (Ios-17pm) material customization. The six
    /// live axes are the authored set with real DynamicResource consumers
    /// (glass/glass-border/shadow opacity stay parked until a live key
    /// exists — see CrystalFluidSettings). The baseline consts remain the
    /// single number home for the defaults.
    /// </summary>
    public class ThemeCustomizationSettings
    {
        /// <summary>Water surface opacity (clamped 0.35–1.25).</summary>
        public double WaterOpacity { get; set; } =
            CrystalFluidSettings.BaselineWaterOpacity;

        /// <summary>Water flow speed (clamped 0.60–1.50).</summary>
        public double WaterSpeed { get; set; } =
            CrystalFluidSettings.BaselineWaterSpeed;

        /// <summary>Wave amplitude in DIPs (clamped 1.5–4.5).</summary>
        public double WaveAmplitude { get; set; } =
            CrystalFluidSettings.BaselineWaveAmplitude;

        /// <summary>Caustic light-pool intensity (clamped 0.25–1.00).</summary>
        public double CausticIntensity { get; set; } =
            CrystalFluidSettings.BaselineCausticIntensity;

        /// <summary>Bubble intensity (clamped 0.25–1.00).</summary>
        public double BubbleIntensity { get; set; } =
            CrystalFluidSettings.BaselineBubbleIntensity;

        /// <summary>§13 specular reflection opacity (clamped 0.15–1.00).</summary>
        public double ReflectionOpacity { get; set; } =
            CrystalFluidSettings.BaselineReflectionOpacity;

        /// <summary>Last preset that wrote these values —
        /// "Default" | "Calm" | "Intense", or "" after a manual tweak.
        /// Identity only (view/audit); the axes above are the truth.</summary>
        public string CrystalPreset { get; set; } = "Default";
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

        // ── Step 9: Target pricing default (not a visibility flag) ─
        // The expected profit margin (%) that Step 9's target uplift
        // (× 1.15 → × (1 + margin/100)) and Step 4's margin boxes use as
        // their DEFAULT on new invoices. 15% keeps the approved formula
        // byte-identical on fresh installs. Saved per invoice at creation
        // time; see Services.Features.Step9PricingDefaults for the live
        // runtime mirror.
        public decimal ExpectedProfitMarginPercent { get; set; } = 15m;
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
        /// <summary>
        /// Vertical resolution: 720, 1080, 1440 or 2160.
        /// Seed default is 2160 (4K): YouTube assigns the VP9/AV1 codec
        /// ladder at ≥1440p, which is THE decisive factor for how sharp an
        /// upload looks after their re-encode. A persistently CHOSEN value
        /// by a lower end always wins — this only seeds fresh installs.
        /// </summary>
        public int QualityHeight { get; set; } = 2160;

        /// <summary>
        /// Target frame rate: 30, 40, 50 or 60. Seed default 60: YouTube
        /// gives 2160p60 material its top bitrate tier; the adaptive
        /// capture throttle still protects UI responsiveness at record
        /// time, and duration stays exact via the save-time -itsscale
        /// re-time (frames written one-per-capture, then rescaled to the
        /// real elapsed length on stop).
        /// </summary>
        public int Fps { get; set; } = 60;

        /// <summary>
        /// Encoder quality tier (PHASE 4, 2026-09-14): 0 = Standard,
        /// 1 = High (default), 2 = Very High. Maps to an x264 CRF
        /// baseline — High keeps ERP UI text sharp at reasonable file
        /// size; Very High reproduces the pre-Phase-4 CRFs exactly.
        /// Legacy configs without this field hydrate to the property
        /// default (1 = High); out-of-range values are clamped on load.
        /// </summary>
        public int QualityTier { get; set; } = 1;

        /// <summary>
        /// Encoder request (PHASE 5, 2026-09-14): 0 = Auto (preferred
        /// hardware H.264 → CPU libx264), 1 = CPU, 2 = NVIDIA (NVENC),
        /// 3 = Intel (QSV). Legacy configs without this field hydrate
        /// to Auto; out-of-range values are clamped on load. An
        /// unavailable hardware choice falls back at start time and the
        /// reason is logged ([RecEncoderSelect]).
        /// </summary>
        public int Encoder { get; set; } = 0;

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
    /// <summary>
    /// Global language & currency preferences (2026-09-01 system-wide
    /// localization). Language and currency are INDEPENDENT: the user may
    /// run the app in English while amounts display in INR, or in Chinese
    /// with AED — every combination is valid.
    /// </summary>
    public class LocalizationSettings
    {
        /// <summary>BCP-47 code: "en-US" | "ar-AE" | "zh-CN" | "hi-IN" |
        /// "es-ES" | "fr-FR" | "de-DE" | "ru-RU" | "pt-BR" | "tr-TR" |
        /// "ur-PK" | "bn-BD" | "id-ID" | "ja-JP" | "ko-KR".
        /// DEFAULT en-US everywhere (primary language in every country).</summary>
        public string Language { get; set; } = "en-US";

        /// <summary>Display currency (ISO 4217). Empty = follow the
        /// location/geo suggestion. Books always stay in AED — this only
        /// changes what the user SEES.</summary>
        public string CurrencyCode { get; set; } = "";

        /// <summary>When true (default) and the user never chose a
        /// language, the country/region detection offers the local
        /// language (India → हिन्दी, China → 中文, UAE → العربية).
        /// English remains primary regardless — this only applies to the
        /// SUGGESTION, never a forced override.</summary>
        public bool AutoSuggestByRegion { get; set; } = true;

        /// <summary>ISO country last detected (geo / OS region).</summary>
        public string LastKnownCountryIso { get; set; } = "";

        /// <summary>Phone-input default country (drives +971 / +91 …).</summary>
        public string PhoneCountryIso { get; set; } = "";

        /// <summary>Last live FX fetch (informational — cache is the
        /// source).</summary>
        public DateTime LastFxFetchUtc { get; set; }

        /// <summary>
        /// True once the user has been ASKED whether to switch to their
        /// regional language. Set the first time the prompt is shown and
        /// never reset, so the question is asked exactly once per install
        /// regardless of which way it was answered.
        ///
        /// Separate from a "user picked a language" flag on purpose:
        /// answering "keep English" is a real decision and must suppress
        /// the prompt forever, even though it changes nothing.
        /// </summary>
        public bool LanguagePromptShown { get; set; }
    }

    public class AppConfig
    {
        public AiSettings Ai { get; set; } = new AiSettings();
        public bool OnlineFeaturesEnabled { get; set; } = true;
        public AppearanceSettings Appearance { get; set; } = new();

        /// <summary>Local/LAN database pipeline settings.</summary>
        public DataConnectivitySettings DataConnectivity { get; set; } = new();
        public LocalizationSettings Localization { get; set; } = new();

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

        /// <summary>
        /// Sharing Center preferences (providers, defaults, document rules,
        /// template overrides, temp behavior). Additive schema change —
        /// older config.json files hydrate to defaults via
        /// ConfigService.Load's null-coalescing pass. Nothing existing
        /// changes until the operator opens the share composer.
        /// </summary>
        public Services.Sharing.SharingSettings Sharing { get; set; }
            = new Services.Sharing.SharingSettings();
    }
}
