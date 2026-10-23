// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Theme/Eight/EightThemeProfile.cs
// PROJECT: ProGlassAutomation
// MODULE: Eight-theme engine — recipe model
//
// A "profile" is a plain data record. No WPF types, no brushes, no static
// constructors. That matters for two reasons:
//
//   1. TESTABILITY. The whole eight-family contract can be asserted by
//      reading data, with no STA thread and no ResourceDictionary. Every
//      regression guard in Tests/Theme/EightThemeEngineTests.cs is a plain
//      data assertion.
//   2. CCTPURITY. WPF object construction at type-initialiser time is what
//      produced the "themes crash the app on first paint" class of bug in
//      this codebase. Brushes are built by EightThemePainter, at APPLY
//      time, never here.
//
// The five dimensions below are the ones a designer actually changes when
// they say "make it claymorphism" versus "make it swiss":
//
//   Colour     — the hue/chroma skeleton
//   Geometry   — corner radius scale (the single loudest family signal)
//   Elevation  — the shadow model (NONE / OUTSET-PAIR / THREE-STACK /
//                SOFT-AMBIENT / TINTED-GLOW)
//   Material   — translucency, blur, saturation, rim treatment
//   Motion     — durations and easing curves
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Windows.Media;

namespace ProGlassAutomation.Services.Theme.Eight
{
    /// <summary>How a family builds its shadow. This is the single most
    /// identity-defining choice after geometry.</summary>
    public enum EightElevationKind
    {
        /// <summary>No drop shadow at all. Hierarchy is carried purely by
        /// keyline and fill contrast. (Swiss — the style forbids shadows,
        /// and a shadow is decoration.)</summary>
        None = 0,

        /// <summary>One mirrored OUTSET pair — light offset toward the light
        /// source (top-left), dark offset away (bottom-right). Depth comes
        /// from light on a surface the same colour as the page. Requires a
        /// non-shadow edge cue for accessibility. (Neumorphism)</summary>
        Extrude = 1,

        /// <summary>One soft outer drop, tinted toward the surface's OWN hue
        /// rather than neutral grey, plus two INSET shadows (light top-left,
        /// shade bottom-right) for volume. (Claymorphism)</summary>
        Inflate = 2,

        /// <summary>Large, soft, low-opacity ambient shadow with generous
        /// blur. Separation is atmospheric rather than structural.
        /// (Spatial)</summary>
        Ambient = 3,

        /// <summary>Shadow tinted with the accent / spectral hue instead of
        /// neutral ink, so light appears to come FROM the surface.
        /// (Holographic)</summary>
        TintedGlow = 4,

        /// <summary>ONE hard offset, ZERO blur. Not a soft shadow scaled
        /// up — a different mechanism entirely: a translated copy of the
        /// element's own silhouette, which is why it reads as a printed
        /// cut-out rather than as elevation. This is the only kind in
        /// this engine with a hard edge, and the reason a zero-blur
        /// shadow could not be expressed by tuning any of the others.
        /// (Neo-Brutalism)</summary>
        Stamp = 5,

        /// <summary>Exactly one low, wide shadow level for everything.
        /// Deliberately no depth ramp — a bento grid's cells must read as
        /// one flat tray, not as a stack of cards at different heights.</summary>
        FlatSingleLevel = 5,

        /// <summary>Refraction model: a soft shadow paired with a bright
        /// specular rim that catches the light along the top edge.
        /// (Liquid Glass, Glassmorphism)</summary>
        Refractive = 6,
    }

    // ═══════════════════════════════════════════════════════════════════
    //  COLOUR
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>A full surface ramp for one mode (light or dark). Levels are
    /// named after their ROLE, never after a hex value, so a family can be
    /// re-coloured without every call site being touched.</summary>
    public sealed class EightColorRamp
    {
        public EightColorRamp(
            Color window, Color module, Color surface, Color surfaceSecondary,
            Color surfaceDetail, Color chrome, Color dialog, Color dialogInset,
            Color textPrimary, Color textSecondary, Color textMuted,
            Color borderSubtle, Color borderDefault, Color borderStrong,
            Color accent, Color accentForeground, Color shadow, Color focusRing,
            Color? blobIndigo = null, Color? blobViolet = null,
            Color? blobTeal = null)
        {
            Window = window;
            Module = module;
            Surface = surface;
            SurfaceSecondary = surfaceSecondary;
            SurfaceDetail = surfaceDetail;
            Chrome = chrome;
            Dialog = dialog;
            DialogInset = dialogInset;
            TextPrimary = textPrimary;
            TextSecondary = textSecondary;
            TextMuted = textMuted;
            BorderSubtle = borderSubtle;
            BorderDefault = borderDefault;
            BorderStrong = borderStrong;
            Accent = accent;
            AccentForeground = accentForeground;
            Shadow = shadow;
            FocusRing = focusRing;

            // The aurora field. Optional with an Accent fallback so the
            // seven surface-first families need no change: a ramp that
            // does not name its blobs gets the accent, which is exactly
            // as good as a guess, since no other family ever paints one.
            _blobIndigo = blobIndigo;
            _blobViolet = blobViolet;
            _blobTeal = blobTeal;
        }

        private readonly Color? _blobIndigo;
        private readonly Color? _blobViolet;
        private readonly Color? _blobTeal;

        /// <summary>The ANCHOR blob. Roughly half the field weight.</summary>
        public Color BlobIndigo => _blobIndigo ?? Accent;
        /// <summary>Orbiting blob, centre-right.</summary>
        public Color BlobViolet => _blobViolet ?? FocusRing;
        /// <summary>Orbiting blob, lower-right.</summary>
        public Color BlobTeal => _blobTeal ?? Accent;

        /// <summary>Behind everything — the desk the window sits on.</summary>
        public Color Window { get; }
        /// <summary>The module content stage.</summary>
        public Color Module { get; }
        /// <summary>Primary card / work surface.</summary>
        public Color Surface { get; }
        /// <summary>Raised layer, popovers.</summary>
        public Color SurfaceSecondary { get; }
        /// <summary>Inline / nested detail layer.</summary>
        public Color SurfaceDetail { get; }
        /// <summary>Navigation rail and shell chrome.</summary>
        public Color Chrome { get; }
        public Color Dialog { get; }
        public Color DialogInset { get; }
        public Color TextPrimary { get; }
        public Color TextSecondary { get; }
        public Color TextMuted { get; }
        public Color BorderSubtle { get; }
        public Color BorderDefault { get; }
        public Color BorderStrong { get; }
        public Color Accent { get; }
        /// <summary>Text/icon colour that is guaranteed legible ON
        /// <see cref="Accent"/>. Never assumed to be white — neumorphism and
        /// claymorphism both pick a light accent where white would fail.</summary>
        public Color AccentForeground { get; }
        /// <summary>Base shadow hue for this ramp. Not necessarily black —
        /// claymorphism shadows are the surface hue darkened, holographic
        /// shadows are the accent hue.</summary>
        public Color Shadow { get; }
        /// <summary>Focus indicator colour. Separate from Accent so a family
        /// can have a calm accent and still meet WCAG 2.4.11/2.4.13 on its
        /// focus ring.</summary>
        public Color FocusRing { get; }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  GEOMETRY
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>A family's corner-radius scale. Radii are the loudest
    /// identity signal in a UI — a Swiss card at 0px and a clay card at
    /// 36px read as different products before a single pixel of colour is
    /// compared.</summary>
    public sealed class EightGeometry
    {
        public EightGeometry(double control, double card, double large, double hero)
        {
            Control = control;
            Card = card;
            Large = large;
            Hero = hero;
        }

        /// <summary>Buttons, inputs, chips.</summary>
        public double Control { get; }
        /// <summary>Standard card.</summary>
        public double Card { get; }
        /// <summary>Panel / dialog.</summary>
        public double Large { get; }
        /// <summary>Full-bleed hero surface.</summary>
        public double Hero { get; }

        /// <summary>
        /// The bento gutter contract: the gap BETWEEN cells must sit between
        /// roughly half and one full corner radius, and the inner padding of
        /// a cell should be at least the gutter. Swiss uses a 0-radius
        /// scale, which makes the gutter rule collapse to "tight and
        /// ruled" — that is correct, not an oversight.
        /// </summary>
        public double Gutter => Math.Round(Math.Max(Card * 0.66, 8));
    }

    // ═══════════════════════════════════════════════════════════════════
    //  ELEVATION
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>A family's shadow model expressed as data.</summary>
    public sealed class EightElevation
    {
        public EightElevation(
            EightElevationKind kind,
            double lowBlur, double lowDepth, double lowOpacity,
            double highBlur, double highDepth, double highOpacity,
            Color lightStop, Color shadeStop)
        {
            Kind = kind;
            LowBlur = lowBlur;
            LowDepth = lowDepth;
            LowOpacity = lowOpacity;
            HighBlur = highBlur;
            HighDepth = highDepth;
            HighOpacity = highOpacity;
            LightStop = lightStop;
            ShadeStop = shadeStop;
        }

        public EightElevationKind Kind { get; }
        public double LowBlur { get; }
        public double LowDepth { get; }
        public double LowOpacity { get; }
        public double HighBlur { get; }
        public double HighDepth { get; }
        public double HighOpacity { get; }

        /// <summary>The TOP-LEFT highlight stop. Only Extrude uses it, and
        /// using it is mandatory there: the defining trait of neumorphism is
        /// that the surface extrudes FROM a single light source, so a family
        /// that declares Extrude and then paints only one shadow has thrown
        /// away the only thing it was.</summary>
        public Color LightStop { get; }

        /// <summary>The BOTTOM-RIGHT shade stop. For Extrude this is the base
        /// colour darkened ~12%; for Inflate it is the surface's OWN hue
        /// darkened ~15% and NEVER neutral grey — grey shadows are the
        /// single most common mistake in claymorphism and they kill the
        /// moulded read instantly.</summary>
        public Color ShadeStop { get; }

        /// <summary>
        /// Whether this family MUST ship a non-shadow boundary cue. True for
        /// Extrude: a neumorphic edge is a soft shadow a few percent off the
        /// background, which measures around 1.4:1 and structurally fails
        /// WCAG 2.2 SC 1.4.11 (3:1 for non-text boundaries). Shadow alone is
        /// not a state, so the painter always publishes a hairline beside
        /// the shadow for these families.</summary>
        public bool RequiresNonShadowEdge => Kind == EightElevationKind.Extrude;
    }

    // ═══════════════════════════════════════════════════════════════════
    //  MATERIAL
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>How a family treats translucency and its rim.</summary>
    public sealed class EightMaterial
    {
        public EightMaterial(
            EightMaterialKind kind,
            double surfaceAlpha,
            double chromeAlpha,
            double blurRadius,
            double saturationBoost,
            double rimOpacity,
            Color rimTint,
            bool specularRim,
            double innerGlowOpacity,
            bool allowBackdrop)
        {
            Kind = kind;
            SurfaceAlpha = surfaceAlpha;
            ChromeAlpha = chromeAlpha;
            BlurRadius = blurRadius;
            SaturationBoost = saturationBoost;
            RimOpacity = rimOpacity;
            RimTint = rimTint;
            SpecularRim = specularRim;
            InnerGlowOpacity = innerGlowOpacity;
            AllowBackdrop = allowBackdrop;
        }

        public EightMaterialKind Kind { get; }

        /// <summary>Opacity of the primary card fill, 0..1. 1.0 for the solid
        /// families. Glass families run low on purpose: the material is only
        /// worth its cost if something shows through it.</summary>
        public double SurfaceAlpha { get; }

        /// <summary>Opacity of navigation/shell chrome. Separate from the
        /// card fill because a rail that matches its cards stops reading as
        /// chrome.</summary>
        public double ChromeAlpha { get; }

        /// <summary>Backdrop blur radius in device-independent pixels. This
        /// doubles as the DEPTH SIGNAL for the Spatial family — 20px for
        /// chrome, 40px for panels, 60px for overlays, so the material
        /// itself communicates which plane an element is on.</summary>
        public double BlurRadius { get; }

        /// <summary>Extra saturation pushed into the fill so a translucent
        /// surface reads as ALIVE rather than grey. Liquid Glass needs this
        /// badly: without it the heavy blur desaturates the backdrop and the
        /// material looks like dirt on the screen.</summary>
        public double SaturationBoost { get; }

        /// <summary>Opacity of the hairline rim. Solid families still get one
        /// — it is their separator.</summary>
        public double RimOpacity { get; }

        /// <summary>What the rim is tinted with: white for a lit glass edge,
        /// near-black for a hairline on paper, the accent for a foil edge.</summary>
        public Color RimTint { get; }

        /// <summary>True when the rim is a lit specular highlight that should
        /// be brightest along the TOP edge and fall away at the bottom — the
        /// Liquid Glass tell. False when the rim is a flat hairline.</summary>
        public bool SpecularRim { get; }

        /// <summary>Strength of the top-left inner sheen that makes a
        /// surface read as inflated rather than flat.</summary>
        public double InnerGlowOpacity { get; }

        /// <summary>Whether a wallpaper / scene layer may show through at
        /// all. False for every family whose physics needs a controlled
        /// ground: neumorphism (the surface must equal the page or the
        /// extrude dies) and swiss (the grid must stay unviolated).</summary>
        public bool AllowBackdrop { get; }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  MOTION
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>Timing language. Durations are in milliseconds.</summary>
    public sealed class EightMotion
    {
        public EightMotion(double fast, double normal, double slow, string easeOut, string spring)
        {
            Fast = fast;
            Normal = normal;
            Slow = slow;
            EaseOut = easeOut;
            Spring = spring;
        }

        public double Fast { get; }
        public double Normal { get; }
        public double Slow { get; }
        /// <summary>WPF KeySpline control points, four comma-separated
        /// doubles. Published as a sys:String so XAML can consume it.</summary>
        public string EaseOut { get; }
        public string Spring { get; }
    }

    // ═══════════════════════════════════════════════════════════════════
    //  THE RECIPE
    // ═══════════════════════════════════════════════════════════════════

    /// <summary>Everything one theme family is. Immutable after
    /// construction; built once in <see cref="EightThemeCatalog"/>.</summary>
    public sealed class EightThemeProfile
    {
        public EightThemeProfile(
            EightThemeId id,
            string identity,
            string displayName,
            string tagline,
            string summary,
            string materialNote,
            EightColorRamp light,
            EightColorRamp dark,
            EightGeometry geometry,
            EightElevation elevation,
            EightMaterial material,
            EightMotion motion,
            string[] accentPresets,
            string sceneLabel,
            string sceneType)
        {
            Id = id;
            Identity = identity;
            DisplayName = displayName;
            Tagline = tagline;
            Summary = summary;
            MaterialNote = materialNote;
            Light = light;
            Dark = dark;
            Geometry = geometry;
            Elevation = elevation;
            Material = material;
            Motion = motion;
            AccentPresets = accentPresets;
            SceneLabel = sceneLabel;
            SceneType = sceneType;
        }

        public EightThemeId Id { get; }
        /// <summary>The persisted identity string.</summary>
        public string Identity { get; }
        public string DisplayName { get; }
        /// <summary>Five or six words. Shown on the Theme Center card.</summary>
        public string Tagline { get; }
        /// <summary>One sentence on what the family is actually FOR.</summary>
        public string Summary { get; }
        /// <summary>The material mechanics, in plain language. This exists
        /// because "why do my cards look embossed?" is the first question a
        /// user asks after switching to Neumorphism.</summary>
        public string MaterialNote { get; }

        public EightColorRamp Light { get; }
        public EightColorRamp Dark { get; }
        public EightGeometry Geometry { get; }
        public EightElevation Elevation { get; }
        public EightMaterial Material { get; }
        public EightMotion Motion { get; }

        /// <summary>Accent hexes that suit this family. Not decoration —
        /// an iridescent surface and a pastel clay surface need very
        /// different accents, and letting the global accent preset list
        /// apply unchanged is how a Swiss theme ends up neon pink.</summary>
        public string[] AccentPresets { get; }

        /// <summary>Name of this family's built-in scene. Must match an entry
        /// in <see cref="EightThemeScenes"/> or the Theme Center preview
        /// falls back to the default.</summary>
        public string SceneLabel { get; }
        /// <summary>Human caption for the scene tile, so two families never
        /// read as the same backdrop.</summary>
        public string SceneType { get; }

        public EightColorRamp Ramp(bool dark) => dark ? Dark : Light;
    }

    /// <summary>Lookup helpers over the eight recipes. Pure functions over
    /// an immutable table.</summary>
    public static class EightThemeCatalog
    {
        private static readonly Dictionary<string, EightThemeProfile> ByIdentity =
            new(StringComparer.OrdinalIgnoreCase);

        private static readonly List<EightThemeProfile> Ordered = new();

        static EightThemeCatalog()
        {
            foreach (var profile in EightThemeProfiles.Build())
            {
                Ordered.Add(profile);
                ByIdentity[profile.Identity] = profile;
            }
        }

        public static IReadOnlyList<EightThemeProfile> All => Ordered;

        /// <summary>Resolves a persisted identity. Unknown or null values
        /// resolve to the shipped default — NEVER to the first item, because
        /// the first item changes whenever the catalogue is reordered.</summary>
        public static EightThemeProfile Resolve(string? identity)
        {
            if (!string.IsNullOrWhiteSpace(identity) &&
                ByIdentity.TryGetValue(identity!, out var found))
                return found;
            return ByIdentity[EightThemeNames.Default];
        }

        public static bool IsKnown(string? identity) =>
            !string.IsNullOrWhiteSpace(identity) && ByIdentity.ContainsKey(identity!);

        public static EightThemeProfile ById(EightThemeId id) =>
            Ordered[(int)id];
    }
}