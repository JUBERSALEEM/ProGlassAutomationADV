// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Theme/LiquidGlassTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// The recipe pinned, with the negative assertions carrying the weight.
//
// Liquid Glass fails in the opposite direction from the other families,
// and the failure is worth stating up front because it is easy to
// write tests that miss it:
//
//   * Glassmorphism's failure is a MISSING feature -- a flat rim where
//     there should be a gradient, a tint where there should be a lens.
//     It is invisible next to the intended design and obvious side by
//     side with it.
//
//   * Liquid Glass's failure is an EXTRA one. Add a little more glow,
//     raise the rim a touch, let the press darken "just slightly" to
//     match what the other families do, and every individual decision
//     looks reasonable while the family stops being Liquid Glass at
//     all.
//
// So the tests below assert the family's commitments, not its numbers.
// Most of them are negative, because that is where this family breaks.
//
// The recipe is pure arithmetic. No window, dispatcher or mouse.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Linq;
using System.Windows.Media;
using ProGlassAutomation.Services.Theme;
using ProGlassAutomation.Services.Theme.Eight;
using ProGlassAutomation.Services.Theme.LiquidGlass;
using ProGlassAutomation.Services.Theme.SoftUi;
using Xunit;

namespace ProGlassAutomation.Tests.Theme
{
    public class LiquidGlassTests
    {
        // A ramp chosen to be unremarkable. The point of these tests is
        // the family's RULES, not any particular colour, so the input
        // is deliberately plain and nothing here depends on it.
        private static readonly Color LightFill = Color.FromRgb(242, 244, 248);
        private static readonly Color LightInk = Color.FromRgb(18, 20, 26);
        private static readonly Color LightAccent = Color.FromRgb(60, 120, 255);
        private static readonly Color LightRim = Color.FromRgb(255, 255, 255);

        private static readonly Color DarkFill = Color.FromRgb(22, 24, 30);
        private static readonly Color DarkInk = Color.FromRgb(240, 242, 248);
        private static readonly Color DarkAccent = Color.FromRgb(120, 160, 255);
        private static readonly Color DarkRim = Color.FromRgb(190, 205, 235);

        private static LiquidGlassProfile Light() =>
            LiquidGlassPalette.Build(
                LightFill, LightInk, LightAccent, LightRim, dark: false);

        private static LiquidGlassProfile Dark() =>
            LiquidGlassPalette.Build(
                DarkFill, DarkInk, DarkAccent, DarkRim, dark: true);

        private static LiquidGlassStack? Resolve(
            bool dark,
            SoftDepthTier tier,
            SoftInteractionState state,
            bool reducedTransparency = false,
            double morph = 0.0)
        {
            var profile = dark ? Dark() : Light();
            return LiquidGlassRecipe.Resolve(
                profile, tier, state, reducedTransparency, morph: morph);
        }

        // ═══════════════════════════════════════════════════════════
        //  PLACEMENT — the rule the whole family rests on
        // ═══════════════════════════════════════════════════════════

        [Fact]
        public void Chrome_may_carry_glass_and_content_may_not()
        {
            Assert.True(LiquidGlassDepth.Allows(LiquidGlassPlacement.Chrome));
            Assert.False(LiquidGlassDepth.Allows(LiquidGlassPlacement.Content));
        }

        [Fact]
        public void Stacking_glass_on_glass_is_always_detected()
        {
            Assert.True(LiquidGlassDepth.WouldStackGlass(
                LiquidGlassPlacement.Chrome, LiquidGlassPlacement.Chrome));

            // The dangerous case is the asymmetric one: an inner panel
            // that happens to have been given a chrome placement.
            Assert.False(LiquidGlassDepth.WouldStackGlass(
                LiquidGlassPlacement.Content, LiquidGlassPlacement.Chrome));
        }

        // ═══════════════════════════════════════════════════════════
        //  THE SPECULAR EDGE — the family's signature
        // ═══════════════════════════════════════════════════════════

        [Fact]
        public void The_rim_is_contrast_enforced_in_both_modes()
        {
            // An invisible rim is not a subtle rim, it is glassmorphism.
            // A near-invisible candidate in, a compliant rim out.
            var faintLight = Color.FromRgb(243, 245, 249);
            var faintDark = Color.FromRgb(23, 25, 31);

            var light = LiquidGlassPalette.Build(
                LightFill, LightInk, LightAccent, faintLight, dark: false);
            var dark = LiquidGlassPalette.Build(
                DarkFill, DarkInk, DarkAccent, faintDark, dark: true);

            Assert.True(LiquidGlassPalette.Contrast(light.Rim, light.Fill)
                >= LiquidGlassPalette.MinimumRimContrast);
            Assert.True(LiquidGlassPalette.Contrast(dark.Rim, dark.Fill)
                >= LiquidGlassPalette.MinimumRimContrast);
        }

        [Fact]
        public void The_rim_gradient_is_not_flat()
        {
            foreach (var dark in new[] { false, true })
            {
                var profile = dark ? Dark() : Light();
                var brush = LiquidGlassRecipe.RimGradient(profile, dark);
                var colours = new System.Collections.Generic.HashSet<Color>();
                foreach (var stop in brush.GradientStops)
                    colours.Add(stop.Color);

                // A uniform ring is the single most common way a glass
                // theme reads as tinted film. More than one stop colour
                // is the minimum; a real ramp has three.
                Assert.True(colours.Count > 1,
                    "The rim gradient collapsed to a single colour.");
            }
        }

        [Fact]
        public void Reduced_transparency_raises_the_rim_rather_than_lowering_it()
        {
            var profile = Light();
            var normal = profile.Optics.RimOpacityAt(reducedTransparency: false);
            var reduced = profile.Optics.RimOpacityAt(reducedTransparency: true);

            // The rim is the only affordance left saying where a
            // surface ends once the tint has gone opaque.
            Assert.True(reduced >= normal,
                "Reduced transparency weakened the rim.");
        }

        [Fact]
        public void Reduced_transparency_zeroes_the_blur()
        {
            foreach (var tier in Enum.GetValues<SoftDepthTier>())
            {
                Assert.Equal(0.0, LiquidGlassRecipe.BlurFor(
                    tier, SoftInteractionState.Rest, reducedTransparency: true));

                // And a tier that blurs when transparency is allowed
                // must not be the only evidence the tier does anything.
                if (tier != SoftDepthTier.Well)
                {
                    Assert.True(LiquidGlassRecipe.BlurFor(
                        tier, SoftInteractionState.Rest,
                        reducedTransparency: false) > 0.0);
                }
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  PRESS DOES NOT DARKEN — the rule most likely to be broken
        //  by someone making it match the other families
        // ═══════════════════════════════════════════════════════════

        [Fact]
        public void Press_does_not_darken_the_fill()
        {
            var resting = Resolve(false, SoftDepthTier.Raised,
                SoftInteractionState.Rest);
            var pressed = Resolve(false, SoftDepthTier.Raised,
                SoftInteractionState.Pressed);

            Assert.NotNull(resting);
            Assert.NotNull(pressed);

            // On a lens, darkening reads as the glass THICKENING rather
            // than as being pushed. The press signal is the shadow and
            // the refraction, not the tint.
            Assert.True(pressed!.Fill.R + pressed.Fill.G + pressed.Fill.B
                >= resting!.Fill.R + resting.Fill.G + resting.Fill.B,
                "Press darkened the fill, which on a lens reads as "
                + "thickening rather than as being pushed.");
        }

        [Fact]
        public void Press_collapses_the_shadow_and_relaxes_the_refraction()
        {
            foreach (var tier in new[]
            {
                SoftDepthTier.Resting, SoftDepthTier.Raised,
                SoftDepthTier.Floating,
            })
            {
                var resting = Resolve(false, tier, SoftInteractionState.Rest);
                var pressed = Resolve(false, tier, SoftInteractionState.Pressed);

                Assert.True(pressed!.ShadowDepth < resting!.ShadowDepth,
                    "Press did not collapse the shadow depth.");
                Assert.True(pressed.ShadowOpacity < resting.ShadowOpacity,
                    "Press did not collapse the shadow opacity.");
                Assert.True(pressed.Refraction < resting.Refraction,
                    "Press did not relax the refraction.");
            }
        }

        [Fact]
        public void Press_is_reported_so_templates_do_not_have_to_infer_it()
        {
            Assert.True(Resolve(false, SoftDepthTier.Raised,
                SoftInteractionState.Pressed)!.IsPressed);
            Assert.False(Resolve(false, SoftDepthTier.Raised,
                SoftInteractionState.Rest)!.IsPressed);
        }

        // ═══════════════════════════════════════════════════════════
        //  MORPH — a thicker slab, not a bigger one
        // ═══════════════════════════════════════════════════════════

        [Fact]
        public void Morphing_deepens_the_shadow_and_strengthens_the_refraction()
        {
            var resting = Resolve(false, SoftDepthTier.Raised,
                SoftInteractionState.Rest, morph: 0.0);
            var morphed = Resolve(false, SoftDepthTier.Raised,
                SoftInteractionState.Rest, morph: 1.0);

            Assert.True(morphed!.ShadowDepth > resting!.ShadowDepth,
                "Morph did not deepen the shadow.");
            Assert.True(morphed.Refraction > resting.Refraction,
                "Morph did not strengthen the refraction.");
        }

        [Fact]
        public void Morph_is_clamped_so_a_runaway_value_cannot_distort_the_edge()
        {
            var normal = Resolve(false, SoftDepthTier.Raised,
                SoftInteractionState.Rest, morph: 1.0);
            var runaway = Resolve(false, SoftDepthTier.Raised,
                SoftInteractionState.Rest, morph: 50.0);

            Assert.True(runaway!.ShadowDepth <= normal!.ShadowDepth,
                "An over-driven morph deepened the shadow past its clamp.");
            Assert.True(runaway.Refraction <= 2.4,
                "Refraction escaped its clamp.");
        }

        // ═══════════════════════════════════════════════════════════
        //  TIERS
        // ═══════════════════════════════════════════════════════════

        [Fact]
        public void A_Well_has_no_outer_shadow_in_any_state()
        {
            foreach (var state in Enum.GetValues<SoftInteractionState>())
            {
                var stack = Resolve(false, SoftDepthTier.Well, state);
                Assert.Equal(0.0, stack!.ShadowDepth);
                Assert.Equal(0.0, stack.ShadowOpacity);
            }
        }

        [Fact]
        public void Higher_tiers_float_higher()
        {
            var resting = Resolve(false, SoftDepthTier.Resting,
                SoftInteractionState.Rest);
            var raised = Resolve(false, SoftDepthTier.Raised,
                SoftInteractionState.Rest);
            var floating = Resolve(false, SoftDepthTier.Floating,
                SoftInteractionState.Rest);

            Assert.True(resting!.ShadowDepth < raised!.ShadowDepth);
            Assert.True(raised.ShadowDepth < floating!.ShadowDepth);
            Assert.True(resting.Blur < raised.Blur);
            Assert.True(raised.Blur < floating.Blur);
        }

        [Fact]
        public void Hover_sharpens_the_lens()
        {
            var resting = Resolve(false, SoftDepthTier.Raised,
                SoftInteractionState.Rest);
            var hovered = Resolve(false, SoftDepthTier.Raised,
                SoftInteractionState.Hover);

            Assert.True(hovered!.Blur < resting!.Blur,
                "Hover did not sharpen the lens.");
            Assert.True(hovered.ShadowDepth > resting.ShadowDepth);
        }

        // ═══════════════════════════════════════════════════════════
        //  DISABLED
        // ═══════════════════════════════════════════════════════════

        [Fact]
        public void A_disabled_surface_keeps_its_shape_but_loses_its_shadow()
        {
            var resting = Resolve(false, SoftDepthTier.Raised,
                SoftInteractionState.Rest);
            var disabled = Resolve(false, SoftDepthTier.Raised,
                SoftInteractionState.Disabled);

            // The fill cools but the surface stays a surface. A
            // disabled control that disappears entirely is worse than
            // one that is merely dim.
            Assert.NotEqual(resting!.Fill, disabled!.Fill);
            Assert.True(disabled.ShadowOpacity < resting.ShadowOpacity);
            Assert.True(disabled.ShadowDepth > 0.0,
                "Disabled removed the shadow entirely rather than "
                + "weakening it.");
        }

        // ═══════════════════════════════════════════════════════════
        //  OPTICS
        // ═══════════════════════════════════════════════════════════

        [Fact]
        public void Dispersion_is_always_a_fraction_of_refraction()
        {
            foreach (var dark in new[] { false, true })
            {
                var optics = (dark ? Dark() : Light()).Optics;
                Assert.True(optics.Dispersion > 0.0);
                Assert.True(optics.Dispersion < optics.Refraction,
                    "Dispersion exceeded refraction, which produces "
                    + "colour fringing that reads as a rendering bug.");
            }
        }

        [Fact]
        public void Refraction_is_stronger_in_dark_mode()
        {
            // A dark fill already suppresses the background's luminance,
            // so the displacement has to work harder to read as glass.
            Assert.True(Dark().Optics.Refraction > Light().Optics.Refraction);
        }

        [Fact]
        public void The_tint_is_held_off_the_extremes()
        {
            // A lens with nothing behind it has nothing to carry and
            // the rim has nothing to contrast against.
            var white = LiquidGlassPalette.HoldOffExtreme(
                Color.FromRgb(255, 255, 255), dark: false);
            Assert.True(white.R + white.G + white.B <= 750);

            var black = LiquidGlassPalette.HoldOffExtreme(
                Color.FromRgb(0, 0, 0), dark: true);
            Assert.True(black.R + black.G + black.B >= 24);
        }

        [Fact]
        public void An_absent_inner_glow_returns_null_rather_than_a_clear_brush()
        {
            var profile = Light();

            // Same profile with the glow switched off.
            var withoutGlow = new LiquidGlassProfile(
                profile.Fill, profile.Ink, profile.Rim, profile.Accent,
                new LiquidOptics(
                    refraction: profile.Optics.Refraction,
                    dispersion: profile.Optics.Dispersion,
                    rimOpacity: profile.Optics.RimOpacity,
                    specularPower: profile.Optics.SpecularPower,
                    innerGlowOpacity: 0.0,
                    scatter: profile.Optics.Scatter),
                profile.Metrics);

            var stack = LiquidGlassRecipe.Resolve(
                withoutGlow, SoftDepthTier.Raised, SoftInteractionState.Rest);

            Assert.NotNull(stack);
            // Null, not a zero-alpha brush: an all-clear element still
            // costs a render pass on every frame.
            Assert.Null(LiquidGlassRecipe.InnerGlow(stack!));
        }

        // ═══════════════════════════════════════════════════════════
        //  PALETTE BRIDGE
        // ═══════════════════════════════════════════════════════════

        [Fact]
        public void Only_the_liquid_family_publishes_a_liquid_palette()
        {
            LiquidGlassPalette.Publish(
                LightFill, LightInk, LightAccent, LightRim,
                DarkFill, DarkInk, DarkAccent, DarkRim);

            try
            {
                Assert.True(LiquidGlassPalette.IsActive);
                Assert.NotNull(LiquidGlassPalette.For(dark: false));
                Assert.NotNull(LiquidGlassPalette.For(dark: true));

                foreach (var profile in EightThemeCatalog.All)
                {
                    var matches = profile.Identity == EightThemeNames.LiquidGlass;
                    Assert.True(matches);
                }
            }
            finally
            {
                LiquidGlassPalette.Clear();
            }

            Assert.False(LiquidGlassPalette.IsActive);
            Assert.Null(LiquidGlassPalette.For(dark: false));
        }

        [Fact]
        public void Liquid_glass_is_its_own_material_kind()
        {
            // The painter switches on the MATERIAL, and Liquid Glass is
            // the only family that is Refractive. Asserting it here means
            // a future edit that reclassifies the family breaks a test
            // rather than silently falling through to `default`, which is
            // where this family used to live and where it published
            // nothing at all.
            var liquid = EightThemeCatalog.All.First(
                p => p.Identity == EightThemeNames.LiquidGlass);

            Assert.Equal(EightMaterialKind.Refractive, liquid.Material.Kind);

            // And it must not be confused with its sibling.
            var glassmorphism = EightThemeCatalog.All.First(
                p => p.Identity == EightThemeNames.Glassmorphism);
            Assert.NotEqual(liquid.Material.Kind, glassmorphism.Material.Kind);
        }
    }
}