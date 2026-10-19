// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Theme/NeumorphismTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// The dual-shadow recipe, pinned.
//
// Like the clay tests, every assertion here is a regression against a
// specific way this style gets faked. Neumorphism is unusually easy to
// fake because it is famous for being soft: a rounded grey box with one
// drop shadow passes a screenshot review and fails every test here,
// because every one of the failures is structural.
//
// The recipe is pure arithmetic, so none of this needs a window, a
// dispatcher or a mouse.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using ProGlassAutomation.Services.Theme;
using ProGlassAutomation.Services.Theme.Eight;
using ProGlassAutomation.Services.Theme.Clay;
using ProGlassAutomation.Services.Theme.Neu;
using ProGlassAutomation.Services.Theme.SoftUi;
using Xunit;

namespace ProGlassAutomation.Tests.Theme
{
    public class NeumorphismTests
    {
        // The ramp's real base, which is the canonical pale blue-grey.
        private static readonly Color LightBase =
            Color.FromRgb(0xE0, 0xE5, 0xEC);
        private static readonly Color DarkBase =
            Color.FromRgb(0x2A, 0x2D, 0x32);

        private static NeuProfile Light() =>
            NeuVisualFactory.BuildProfile(
                LightBase, Colors.White, Colors.Black, dark: false);

        private static NeuProfile Dark() =>
            NeuVisualFactory.BuildProfile(
                DarkBase, Colors.White, Colors.Black, dark: true);

        private static NeuShadowStack Resolve(
            SoftDepthTier tier, SoftInteractionState state,
            double radius = 16, bool dark = false) =>
            new NeuRecipe(dark ? Dark() : Light(), dark)
                .Resolve(tier, state, radius);

        // ── the mirrored pair ────────────────────────────────────────

        [Fact]
        public void A_resting_surface_carries_both_halves_of_the_pair()
        {
            // Two shadows, mirrored, on the same shape. One is the whole
            // difference between neumorphism and "a grey card".
            var stack = Resolve(SoftDepthTier.Resting,
                SoftInteractionState.Rest);

            Assert.NotNull(stack.Light);
            Assert.NotNull(stack.Shade);
            Assert.True(stack.IsExtruded);
            Assert.True(stack.HasOutset);

            // ...and NOTHING inset. An extruded object that is also
            // dented is not one object.
            Assert.Null(stack.InsetLight);
            Assert.Null(stack.InsetShade);
        }

        [Fact]
        public void The_two_halves_point_opposite_ways()
        {
            var stack = Resolve(SoftDepthTier.Resting,
                SoftInteractionState.Rest);

            Assert.Equal(SoftLight.HighlightDirection,
                stack.Light!.Direction);
            Assert.Equal(SoftLight.ShadowDirection,
                stack.Shade!.Direction);

            // Mismatched directions are not a subtle error — they destroy
            // the extrusion instantly, because the eye reads the pair as
            // two unrelated smudges rather than as one lit surface.
            Assert.NotEqual(stack.Light.Direction, stack.Shade.Direction);
        }

        [Fact]
        public void Both_halves_derive_from_ONE_depth_value()
        {
            // The single most common fudge. Give the highlight and the
            // shade different offsets and the object stops looking
            // moulded and starts looking soft-edged.
            var stack = Resolve(SoftDepthTier.Raised,
                SoftInteractionState.Rest);

            Assert.Equal(stack.Light!.ShadowDepth, stack.Shade!.ShadowDepth);
            Assert.Equal(stack.Light.BlurRadius, stack.Shade.BlurRadius);
        }

        [Fact]
        public void Blur_is_roughly_twice_the_offset()
        {
            // Harsher than about 2x and it stops reading as a soft shadow
            // and starts reading as an edge, which destroys the effect
            // just as thoroughly as making it too faint.
            foreach (var tier in new[]
            {
                SoftDepthTier.Resting, SoftDepthTier.Raised,
                SoftDepthTier.Floating,
            })
            {
                var stack = Resolve(tier, SoftInteractionState.Rest);
                double ratio = stack.Light!.BlurRadius / stack.Light.ShadowDepth;
                Assert.InRange(ratio, 1.6, 2.6);
            }
        }

        // ── the press is an inversion ────────────────────────────────

        [Fact]
        public void Pressing_inverts_the_pair_into_the_surface()
        {
            // The one motion neumorphism gets right: a physical dent
            // reads as "pressed" with no colour change at all.
            var rest = Resolve(SoftDepthTier.Raised,
                SoftInteractionState.Rest);
            var pressed = Resolve(SoftDepthTier.Raised,
                SoftInteractionState.Pressed);

            Assert.True(rest.IsExtruded);
            Assert.False(pressed.IsExtruded);
            Assert.True(pressed.Pressed);

            // The outset is GONE, not reduced. An extruded object and a
            // dented one are different objects; rendering both is the
            // classic mistake.
            Assert.Null(pressed.Light);
            Assert.Null(pressed.Shade);

            Assert.NotNull(pressed.InsetLight);
            Assert.NotNull(pressed.InsetShade);
            Assert.True(pressed.InsetReach > 0);
        }

        [Fact]
        public void The_dent_travels_less_than_the_lift()
        {
            // A dent has less room than a lift before it stops looking
            // like either of them.
            var rest = Resolve(SoftDepthTier.Raised,
                SoftInteractionState.Rest);
            var pressed = Resolve(SoftDepthTier.Raised,
                SoftInteractionState.Pressed);

            Assert.True(pressed.InsetShade!.ShadowDepth
                     < rest.Shade!.ShadowDepth,
                "the inset must be tighter than the outset, or the press "
                + "reads as the object growing.");
        }

        [Fact]
        public void Press_also_moves_the_fill_because_shadow_alone_is_not_a_state()
        {
            // WCAG 1.4.1 excludes low-contrast non-textual cues, and a
            // shadow inversion is exactly that. In a style with no colour
            // boundary at all, the fill shift is the only state cue some
            // users will ever perceive.
            var pressed = Resolve(SoftDepthTier.Raised,
                SoftInteractionState.Pressed);

            Assert.True(pressed.FillShift > 0,
                "a press must not be signalled by depth alone.");
            Assert.Equal(
                SoftUiAccessibility.PressedFillShift,
                pressed.FillShift, 3);
        }

        // ── focus is never a shadow swap ─────────────────────────────

        [Fact]
        public void Focus_is_never_signalled_by_depth()
        {
            // A raised-versus-flat shadow is the wrong signal, full stop:
            // it is non-textual, it is low contrast, and it is invisible
            // to anyone with reduced contrast sensitivity. Focus also
            // gets its own construction here, which is the correct
            // reading for a toggle track — a focused cavity is a cavity.
            foreach (var tier in new[]
            {
                SoftDepthTier.Well, SoftDepthTier.Resting,
                SoftDepthTier.Raised,
            })
            {
                Assert.Equal(tier, SoftDepthRules.Resolve(
                    tier, SoftInteractionState.Focused));
                Assert.False(Resolve(tier, SoftInteractionState.Focused)
                    .Pressed);
            }
        }

        [Fact]
        public void Hover_does_not_spend_the_inversion()
        {
            // The convex-to-concave transition is this style's most
            // powerful feedback and is therefore reserved for press. If
            // everything dented on hover, nothing would read as pressed.
            var hover = Resolve(SoftDepthTier.Raised,
                SoftInteractionState.Hover);

            Assert.False(hover.Pressed);
            Assert.True(hover.IsExtruded);
            Assert.Null(hover.InsetLight);
        }

        // ── the defining constraint ──────────────────────────────────

        [Fact]
        public void The_base_is_forced_off_pure_white_and_pure_black()
        {
            // A precondition, not a preference. A neumorphic surface on
            // pure white has nowhere to put the highlight and reads as a
            // flat outline; on pure black it has nowhere to put the
            // shade. Both look instantly, recognisably wrong.
            var onWhite = NeuVisualFactory.BuildProfile(
                Colors.White, Colors.White, Colors.Black, dark: false);
            var onBlack = NeuVisualFactory.BuildProfile(
                Colors.Black, Colors.White, Colors.Black, dark: true);

            Assert.True(Luminance(onWhite.BaseFill) < 0.90,
                "a pure white base was accepted.");
            Assert.True(Luminance(onBlack.BaseFill) > 0.02,
                "a pure black base was accepted.");
        }

        [Fact]
        public void Dark_mode_stops_are_greys_never_white_or_black()
        {
            // Pure white on a near-black surface reads as a hard specular
            // flare rather than diffuse bounce, and pure black shade
            // disappears entirely. Both are the classic dark-mode tell
            // that a neumorphic palette was light-mode flipped.
            var dark = Dark();

            Assert.True(dark.LightStop.R < 255 || dark.LightStop.G < 255,
                "the dark highlight is pure white — it will flare.");
            Assert.True(dark.DarkStop.R > 0 || dark.DarkStop.G > 0,
                "the dark shade is pure black — it will vanish.");

            // ...and both still respond to the light: the highlight is
            // lighter than the base and the shade is darker.
            Assert.True(Luminance(dark.LightStop) > Luminance(dark.BaseFill));
            Assert.True(Luminance(dark.DarkStop) < Luminance(dark.BaseFill));
        }

        // ── depth ────────────────────────────────────────────────────

        [Fact]
        public void Depth_tiers_increase_monotonically()
        {
            var resting = Resolve(SoftDepthTier.Resting,
                SoftInteractionState.Rest);
            var raised = Resolve(SoftDepthTier.Raised,
                SoftInteractionState.Rest);
            var floating = Resolve(SoftDepthTier.Floating,
                SoftInteractionState.Rest);

            Assert.True(raised.Light!.ShadowDepth
                     > resting.Light!.ShadowDepth);
            Assert.True(floating.Light.ShadowDepth
                     > raised.Light.ShadowDepth);
        }

        [Fact]
        public void A_well_is_concave_and_has_no_outset()
        {
            var well = Resolve(SoftDepthTier.Well,
                SoftInteractionState.Rest);

            Assert.False(well.IsExtruded);
            Assert.False(well.HasOutset);
            Assert.Null(well.Light);
            Assert.Null(well.Shade);
        }

        [Fact]
        public void Dark_mode_carries_depth_in_the_fill()
        {
            // On a near-black page a shadow has almost no contrast to
            // give, so every tier would collapse into one flat shape.
            foreach (var tier in new[]
            {
                SoftDepthTier.Resting, SoftDepthTier.Raised,
                SoftDepthTier.Floating,
            })
            {
                Assert.True(
                    Resolve(tier, SoftInteractionState.Rest, dark: true)
                        .FillLift > 0,
                    $"{tier}: dark mode needs a fill lift.");
                Assert.Equal(0.0,
                    Resolve(tier, SoftInteractionState.Rest, dark: false)
                        .FillLift);
            }
        }

        [Fact]
        public void Dark_mode_tightens_the_offsets()
        {
            // A large shadow on a dark surface reads as a smudge long
            // before it reads as volume.
            var light = Resolve(SoftDepthTier.Raised,
                SoftInteractionState.Rest);
            var dark = Resolve(SoftDepthTier.Raised,
                SoftInteractionState.Rest, dark: true);

            Assert.True(dark.Light!.ShadowDepth < light.Light!.ShadowDepth,
                "the dark extrusion must be tighter.");
        }

        // ── engine rule 4 ────────────────────────────────────────────

        [Fact]
        public void Shadows_stay_inside_the_engine_bounds()
        {
            foreach (var dark in new[] { false, true })
            {
                foreach (var tier in Enum.GetValues<SoftDepthTier>())
                {
                    foreach (var state
                        in Enum.GetValues<SoftInteractionState>())
                    {
                        var s = Resolve(tier, state, radius: 24, dark: dark);
                        foreach (var layer in new[]
                        {
                            s.Light, s.Shade, s.InsetLight, s.InsetShade,
                        })
                        {
                            if (layer is null) continue;
                            Assert.InRange(layer.BlurRadius, 0, 56);
                            Assert.InRange(layer.ShadowDepth, 0, 12);
                            Assert.InRange(layer.Opacity, 0, 1);
                        }
                    }
                }
            }
        }

        // ── the shared light ─────────────────────────────────────────

        [Fact]
        public void Clay_and_neumorphism_are_lit_by_the_same_light()
        {
            // The bug this exists to prevent. An earlier draft kept the
            // light in each family's own file: it compiled, passed review,
            // and produced highlights pointing one way on the clay screen
            // and another on the neumorphic one. Soft UI lives or dies on
            // the light being consistent screen-wide, and switching themes
            // is exactly when the user is looking at it.
            Assert.Equal(SoftLight.ShadowDirection, 315.0);
            Assert.Equal(SoftLight.HighlightDirection, 135.0);

            // Clay's shade and neumorphism's shade are the same bearing.
            Assert.Equal(
                SoftLight.ShadowDirection,
                Resolve(SoftDepthTier.Resting, SoftInteractionState.Rest)
                    .Shade!.Direction);
        }

        // ── the accessibility floor ──────────────────────────────────

        [Fact]
        public void The_shadow_alone_cannot_pass_non_text_contrast()
        {
            // Stated as a test because it is the reason the edge exists.
            // The canonical neumorphic edge measures ~1.4:1 against the
            // surface; WCAG 2.2 SC 1.4.11 wants 3:1. You cannot tune it up
            // — push the shadows far enough apart to pass and they stop
            // reading as shadows on a shared surface, at which point you
            // are no longer building neumorphism.
            var profile = Light();
            double shadow = RelativeContrast(
                profile.DarkStop, profile.BaseFill);
            Assert.True(shadow < 3.0,
                "if this ever passes, the edge can be reconsidered.");

            // ...which is why the edge is a separate, opaque mechanism.
            var pen = SoftUiAccessibility.BuildEdge(
                profile.BaseFill, dark: false);
            Assert.NotNull(pen);
            Assert.True(pen!.Thickness >= 1.0);
        }

        [Fact]
        public void The_focus_ring_is_real_and_outside_the_shadow()
        {
            Assert.True(SoftUiAccessibility.FocusRingWidth >= 2.0);
            Assert.True(SoftUiAccessibility.FocusRingOffset > 0);
            Assert.True(SoftUiAccessibility.MinHitTarget >= 44);
        }

        // ── palette plumbing ─────────────────────────────────────────

        [Fact]
        public void Only_the_neumorphic_family_publishes_a_neu_palette()
        {
            foreach (var profile in EightThemeCatalog.All)
            {
                NeuPalette.Publish(
                    LightBase, DarkBase, Colors.White, Colors.Black);

                EightThemePainter.Apply(
                    new ResourceDictionary(), profile,
                    dark: false, accent: Colors.SteelBlue);

                bool shouldBeNeu =
                    profile.Material.Kind == EightMaterialKind.Extruded;
                Assert.Equal(shouldBeNeu, NeuPalette.IsActive);

                if (shouldBeNeu)
                {
                    Assert.NotNull(NeuPalette.Light);
                    Assert.NotNull(NeuPalette.Dark);
                }
                else
                {
                    Assert.Null(NeuPalette.Light);
                    Assert.Null(NeuPalette.Dark);
                }
            }

            NeuPalette.Clear();
        }

        [Fact]
        public void The_two_soft_palettes_are_never_both_active()
        {
            // A neumorphic control holding a clay palette (or the
            // reverse) renders the wrong construction under a family it
            // does not belong to, which looks exactly like a theme change
            // that half-happened.
            foreach (var profile in EightThemeCatalog.All)
            {
                EightThemePainter.Apply(
                    new ResourceDictionary(), profile,
                    dark: false, accent: Colors.SteelBlue);

                Assert.False(
                    ClayPalette.IsActive && NeuPalette.IsActive,
                    $"{profile.Identity}: both soft palettes were published.");
            }

            ClayPalette.Clear();
            NeuPalette.Clear();
        }

        // ── helpers ──────────────────────────────────────────────────

        private static double Luminance(Color c)
        {
            double L(double v)
            {
                v /= 255.0;
                return v <= 0.04045 ? v / 12.92
                                    : Math.Pow((v + 0.055) / 1.055, 2.4);
            }
            return L(c.R) * 0.2126 + L(c.G) * 0.7152 + L(c.B) * 0.0722;
        }

        private static double RelativeContrast(Color a, Color b)
        {
            double la = Luminance(a), lb = Luminance(b);
            double hi = Math.Max(la, lb), lo = Math.Min(la, lb);
            return (hi + 0.05) / (lo + 0.05);
        }
    }
}