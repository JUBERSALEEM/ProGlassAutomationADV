// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Theme/Neu/NeuPalette.cs
// PROJECT: ProGlassAutomation
// MODULE: Neumorphism — palette bridge
//
// Mirrors ClayPalette. The two families keep SEPARATE material palettes
// because their recipes genuinely differ (one outer drop + two insets vs
// a mirrored outset pair), but they share the LIGHT through SoftLight —
// which is the part that must never disagree, because a lighting flip on
// theme change is the failure both styles are defined by.
//
// The painter publishes exactly one of the two and clears the other. That
// is not defensive: a neumorphic control still holding a published
// palette after the user switches to clay keeps drawing extruded pairs
// that no longer match the theme, and it looks like the theme change
// half-happened.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Windows.Media;

namespace ProGlassAutomation.Services.Theme.Neu
{
    /// <summary>The live neumorphic material for the current theme.</summary>
    public static class NeuPalette
    {
        private static NeuProfile? _light;
        private static NeuProfile? _dark;

        /// <summary>True while neumorphism is the active family.</summary>
        public static bool IsActive { get; private set; }

        public static NeuProfile? Light => _light;
        public static NeuProfile? Dark => _dark;

        /// <summary>
        /// Raised after the material is republished or cleared. Handlers
        /// run on the UI thread during a theme swap and must be cheap.
        /// </summary>
        public static event EventHandler? Changed;

        /// <summary>Publishes the material for the neumorphic family.</summary>
        public static void Publish(
            Color lightFill, Color darkFill,
            Color rampLightStop, Color rampDarkStop)
        {
            _light = NeuVisualFactory.BuildProfile(
                lightFill, rampLightStop, rampDarkStop, dark: false);
            _dark = NeuVisualFactory.BuildProfile(
                darkFill, rampLightStop, rampDarkStop, dark: true);
            IsActive = true;
            Changed?.Invoke(null, EventArgs.Empty);
        }

        /// <summary>Clears the material when another family is applied.</summary>
        public static void Clear()
        {
            if (!IsActive && _light is null && _dark is null) return;
            _light = null;
            _dark = null;
            IsActive = false;
            Changed?.Invoke(null, EventArgs.Empty);
        }

        /// <summary>
        /// The material for a mode, or null. Callers handle null rather
        /// than falling back: a control that quietly painted itself flat
        /// because the palette was missing is a bug that never reproduces
        /// on a developer's machine, because there it always works.
        /// </summary>
        public static NeuProfile? For(bool dark) => dark ? _dark : _light;

        /// <summary>Resolves a full construction for a control.</summary>
        public static NeuShadowStack? Resolve(
            SoftUi.SoftDepthTier tier,
            SoftUi.SoftInteractionState state,
            double radius,
            bool dark)
        {
            var profile = For(dark);
            if (profile is null) return null;
            return new NeuRecipe(profile, dark).Resolve(tier, state, radius);
        }
    }
}