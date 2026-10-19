// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Theme/Clay/ClayPalette.cs
// PROJECT: ProGlassAutomation
// MODULE: Claymorphism — palette bridge
//
// The recipe needs a colour to be a recipe FOR. This holds the currently
// published clay material, derived from the active EightThemeProfile's
// light and dark ramps, and tells live controls when it changed.
//
// It exists as a separate concern from the theme engine because the two
// answer different questions. The engine publishes TOKENS — the rounded
// corner, the border hairline, the accent — and those are enough for a
// theme that is a restyle. Clay is not a restyle: it is a construction
// that has to recompute itself whenever a tier or an interaction state
// changes, and that recomputation needs the profile object, not a flat
// bag of strings.
//
// The control subscribes to Changed. Everything it exposes is frozen, so
// a theme swap repaints the visible surfaces and leaves the rest alone.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Windows.Media;

namespace ProGlassAutomation.Services.Theme.Clay
{
    /// <summary>
    /// The live clay material for the current theme.
    /// </summary>
    public static class ClayPalette
    {
        private static ClayProfile? _light;
        private static ClayProfile? _dark;

        /// <summary>
        /// True while claymorphism is the active family. Controls use it
        /// to decide whether to build the clay visual tree at all — an
        /// unused control must cost nothing.
        /// </summary>
        public static bool IsActive { get; private set; }

        /// <summary>The light-mode material, or null when inactive.</summary>
        public static ClayProfile? Light => _light;

        /// <summary>The dark-mode material, or null when inactive.</summary>
        public static ClayProfile? Dark => _dark;

        /// <summary>
        /// Raised after the material is republished or cleared. Handlers
        /// must be cheap; they run on the UI thread during a theme swap.
        /// </summary>
        public static event EventHandler? Changed;

        /// <summary>
        /// Publishes the material for a family. Called by the theme engine
        /// when claymorphism is applied.
        /// </summary>
        /// <param name="lightSurface">Light ramp's surface fill.</param>
        /// <param name="darkSurface">Dark ramp's surface fill.</param>
        /// <param name="shadeHue">
        /// The hue both inner shadows are derived from. In practice the
        /// ramp's Shadow token, which the clay ramp already sets to the
        /// family's own violet rather than to grey.
        /// </param>
        /// <param name="lightHue">The inner highlight's colour.</param>
        public static void Publish(
            Color lightSurface,
            Color darkSurface,
            Color shadeHue,
            Color lightHue)
        {
            _light = ClayVisualFactory.BuildProfile(
                lightSurface, shadeHue, lightHue, dark: false);
            _dark = ClayVisualFactory.BuildProfile(
                darkSurface, shadeHue, lightHue, dark: true);
            IsActive = true;
            Changed?.Invoke(null, EventArgs.Empty);
        }

        /// <summary>
        /// Clears the material when another family is applied. Controls
        /// fall back to their family's plain tokens rather than keeping
        /// a clay visual tree alive under a Swiss or Liquid Glass theme.
        /// </summary>
        public static void Clear()
        {
            if (!IsActive && _light is null && _dark is null) return;
            _light = null;
            _dark = null;
            IsActive = false;
            Changed?.Invoke(null, EventArgs.Empty);
        }

        /// <summary>
        /// The material for a mode, or null when claymorphism is not the
        /// active family. Callers must handle null rather than getting a
        /// silent fallback — a clay surface that quietly painted itself
        /// grey because the palette was missing is a bug that will never
        /// be seen on a developer's machine, because there it always works.
        /// </summary>
        public static ClayProfile? For(bool dark) => dark ? _dark : _light;

        /// <summary>
        /// Resolves a full stack. Convenience for controls, which always
        /// want the three-layer result rather than the profile.
        /// </summary>
        public static ClayShadowStack? Resolve(
            ClayDepthTier tier,
            ClayInteractionState state,
            double radius,
            bool dark,
            Color baseFill,
            bool smallSurface = false)
        {
            var profile = For(dark);
            if (profile is null) return null;
            return new ClayRecipe(profile, dark)
                .Resolve(tier, state, radius, smallSurface);
        }
    }
}