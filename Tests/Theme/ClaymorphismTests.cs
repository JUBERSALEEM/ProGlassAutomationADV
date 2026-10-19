// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Theme/ClaymorphismTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// The clay recipe, pinned.
//
// Everything here is a regression against a specific way clay gets faked.
// A "clay" surface that is really a coloured rectangle with one drop
// shadow passes every visual review and fails all of these, because the
// failures are structural rather than visible in a screenshot.
//
// The recipe is pure arithmetic, so none of this needs a window, a
// dispatcher or a mouse — which is the point of putting the construction
// in data rather than in a template.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using ProGlassAutomation.Services.Theme;
using ProGlassAutomation.Services.Theme.Eight;
using ProGlassAutomation.Services.Theme.Clay;
using ProGlassAutomation.Services.Theme.SoftUi;
using Xunit;

namespace ProGlassAutomation.Tests.Theme
{
    public class ClaymorphismTests
    {
        // The clay ramp's own values, so the tests measure the recipe
        // against the real palette rather than a convenient one.
        private static readonly Color LightSurface =
            Color.FromRgb(0xE4, 0xDB, 0xF6);
        private static readonly Color DarkSurface =
            Color.FromRgb(0x2A, 0x21, 0x45);
        private static readonly Color ShadeHue =
            Color.FromRgb(0x6C, 0x4F, 0xC4);
        private static readonly Color LightHue = Colors.White;

        private static ClayProfile Light() => ClayVisualFactory.BuildProfile(
            LightSurface, ShadeHue, LightHue, dark: false);

        private static ClayProfile Dark() => ClayVisualFactory.BuildProfile(
            DarkSurface, ShadeHue, LightHue, dark: true);

        private static ClayShadowStack Resolve(
            SoftDepthTier tier, SoftInteractionState state,
            double radius = 32, bool small = false, bool dark = false) =>
            new ClayRecipe(dark ? Dark() : Light(), dark)
                .Resolve(tier, state, radius, small);

        // ── the three layers ─────────────────────────────────────────

        [Theory]
        [InlineData(SoftDepthTier.Resting)]
        [InlineData(SoftDepthTier.Raised)]
        [InlineData(SoftDepthTier.Floating)]
        public void Every_raised_tier_paints_all_three_shadows(
            SoftDepthTier tier)
        {
            // The whole style. One outer drop for mass, one inner shade
            // for wall thickness, one inner highlight for the roll. A
            // surface missing the inner shade is a lit rectangle; a
            // surface missing the drop is a sticker.
            var stack = Resolve(tier, SoftInteractionState.Rest);

            Assert.True(stack.HasDrop,
                $"{tier}: no outer drop shadow.");
            Assert.True(stack.DropBlur > 0);
            Assert.True(stack.DropOpacity > 0);

            Assert.True(stack.ShadeStops > 0 && stack.ShadeStops < 1,
                $"{tier}: the inner shade roll is not present.");
            Assert.True(stack.LightStops > 0 && stack.LightStops < 1,
                $"{tier}: the inner highlight roll is not present.");
        }

        [Fact]
        public void A_well_has_no_outer_drop()
        {
            // THE ONE PLACE CLAY INVERTS. A field you are about to type
            // into must recede. Giving it a lift tells the hand "press
            // me", and the first thing pressed is a text field that does
            // nothing. This is the single most common way this style is
            // faked, and it is why so many "clay" forms feel like a
            // keyboard made of pillows.
            var stack = Resolve(
                SoftDepthTier.Well, SoftInteractionState.Rest);

            Assert.False(stack.HasDrop,
                "a well must not float — it is a recess.");
            Assert.False(stack.DropOpacity > 0.001);
            Assert.Equal(0.0, stack.DropBlur);
            Assert.Equal(0.0, stack.DropDepth);

            // ...but it must still have its insets, and the deepest of
            // any tier. The far wall of a recess is thick; the near wall
            // is thin. That asymmetry is what makes a well read as a well.
            Assert.True(stack.ShadeStops > 0);
            Assert.True(stack.LightStops > 0);

            var resting = Resolve(
                SoftDepthTier.Resting, SoftInteractionState.Rest);
            Assert.True(stack.LightStops >= resting.LightStops,
                "a well's inset should reach at least as far as a "
                + "resting surface's.");
        }

        // ── one light, everywhere ────────────────────────────────────

        [Fact]
        public void The_drop_always_falls_away_from_the_light()
        {
            var effect = ClayVisualFactory.Build(
                Resolve(SoftDepthTier.Raised,
                        SoftInteractionState.Rest),
                LightSurface).Drop;

            Assert.NotNull(effect);
            Assert.Equal(SoftLight.ShadowDirection, effect!.Direction);

            // 315° in WPF is up-left, so the shadow lands down-right.
            // Get this backwards and the whole layout looks lit from two
            // places at once, which the eye notices long before it can
            // say what is wrong.
            Assert.True(effect.Direction is > 270 and <= 360,
                "the drop must point away from the top-left light.");
        }

        [Theory]
        [InlineData(SoftDepthTier.Resting, false)]
        [InlineData(SoftDepthTier.Resting, true)]
        [InlineData(SoftDepthTier.Floating, false)]
        [InlineData(SoftDepthTier.Well, false)]
        public void The_two_inner_rolls_face_opposite_ways(
            SoftDepthTier tier, bool dark)
        {
            // Highlight is born top-left, shade is born bottom-right. If
            // they both ran the same way the corner would be lit on one
            // side and dark on the other with nothing between them, and
            // the surface would read as flat.
            var visuals = ClayVisualFactory.Build(
                Resolve(tier, SoftInteractionState.Rest, dark: dark),
                dark ? DarkSurface : LightSurface);

            Assert.NotNull(visuals.Light);
            Assert.NotNull(visuals.Shade);

            // Highlight starts at (0,0) — the top-left corner.
            Assert.Equal(0.0, visuals.Light!.StartPoint.X);
            Assert.Equal(0.0, visuals.Light.StartPoint.Y);
            Assert.Equal(1.0, visuals.Light.EndPoint.X);
            Assert.Equal(1.0, visuals.Light.EndPoint.Y);

            // Shade starts at (1,1) — the bottom-right corner.
            Assert.Equal(1.0, visuals.Shade!.StartPoint.X);
            Assert.Equal(1.0, visuals.Shade.StartPoint.Y);
        }

        [Fact]
        public void Pressing_clay_crosses_the_two_inner_rolls_over()
        {
            // The press is not "smaller" and not "less shadow". It is a
            // change of which side the light is falling on: the object has
            // been pushed into the page, so the underside is now what
            // catches the light.
            var resting = ClayVisualFactory.Build(
                Resolve(SoftDepthTier.Raised, SoftInteractionState.Rest),
                LightSurface);
            var pressed = ClayVisualFactory.Build(
                Resolve(SoftDepthTier.Raised, SoftInteractionState.Pressed),
                LightSurface);

            Assert.False(resting.Light!.StartPoint.Equals(
                pressed.Light!.StartPoint));
            Assert.True(pressed.Light.StartPoint.X == 1.0
                     && pressed.Light.StartPoint.Y == 1.0,
                "the pressed highlight must be born bottom-right.");

            // And the drop collapses toward contact rather than vanishing.
            Assert.True(pressed.Drop!.BlurRadius
                     < resting.Drop!.BlurRadius);
            Assert.True(pressed.Drop.ShadowDepth
                     < resting.Drop.ShadowDepth);
        }

        // ── the shadow belongs to the object ─────────────────────────

        [Fact]
        public void The_shadow_is_tinted_to_the_surface_not_grey()
        {
            // A lavender card with a lavender shadow reads as a lavender
            // object sitting on a page. The same card with a grey shadow
            // reads as a lavender rectangle hovering, and that single
            // substitution is the difference between clay and "pastel
            // with a blur". Grey and black shadows are the default in
            // every shadow tool, which is why they leak in.
            var profile = Light();
            var stack = Resolve(
                SoftDepthTier.Resting, SoftInteractionState.Rest);

            double hue = Saturation(stack.DropColor);
            double sourceHue = Saturation(profile.Shade);

            Assert.True(hue > 0.30,
                $"the drop is desaturated to {hue:0.00} — it has drifted " +
                "toward grey.");
            Assert.True(hue > sourceHue * 0.75,
                $"the drop lost the object's hue ({hue:0.00} vs the " +
                $"surface's {sourceHue:0.00}).");

            // Never black. Black insets on pastel read as dirt.
            Assert.True(stack.DropColor.R > 0
                     && stack.DropColor.G > 0
                     && stack.DropColor.B > 0,
                "the drop has gone to black.");
        }

        [Fact]
        public void The_highlight_is_more_transparent_than_the_shade_is_dark()
        {
            // Asymmetry on purpose. A matched pair reads as plastic — it
            // is the signature of a cheap 3D render, because real diffuse
            // clay responds far more strongly to its own occlusion than to
            // a broad light. Matching them is what makes this look like a
            // glossy button instead of a moulded one.
            var stack = Resolve(
                SoftDepthTier.Resting, SoftInteractionState.Rest);
            var visuals = ClayVisualFactory.Build(stack, LightSurface);

            double lightAlpha = visuals.Light!.GradientStops
                .First().Color.A / 255.0;
            double shadeAlpha = visuals.Shade!.GradientStops
                .Last().Color.A / 255.0;

            Assert.True(lightAlpha < shadeAlpha,
                $"the highlight ({lightAlpha:0.00}) must be weaker than " +
                $"the shade ({shadeAlpha:0.00}); a matched pair is plastic.");
        }

        // ── depth is one number ──────────────────────────────────────

        [Fact]
        public void Depth_tiers_increase_monotonically()
        {
            var resting = Resolve(SoftDepthTier.Resting,
                SoftInteractionState.Rest);
            var raised = Resolve(SoftDepthTier.Raised,
                SoftInteractionState.Rest);
            var floating = Resolve(SoftDepthTier.Floating,
                SoftInteractionState.Rest);

            Assert.True(raised.DropBlur > resting.DropBlur);
            Assert.True(raised.DropDepth > resting.DropDepth);
            Assert.True(floating.DropBlur > raised.DropBlur);
            Assert.True(floating.DropDepth > raised.DropDepth);
            Assert.True(floating.DropOpacity > raised.DropOpacity);
        }

        [Fact]
        public void Hover_promotes_exactly_one_tier_and_ratchets()
        {
            // One tier, not "promote to Raised". A floating modal that
            // promoted on hover would try to leave the top of the scale,
            // and a resting list row that promoted would out-depth the
            // card it sits inside.
            foreach (var tier in new[]
            {
                SoftDepthTier.Well, SoftDepthTier.Resting,
                SoftDepthTier.Raised,
            })
            {
                Assert.Equal(
                    SoftDepthRules.Promote(tier),
                    SoftDepthRules.Resolve(tier, SoftInteractionState.Hover));
            }

            Assert.Equal(
                SoftDepthTier.Floating,
                SoftDepthRules.Resolve(
                    SoftDepthTier.Floating, SoftInteractionState.Hover));
        }

        [Fact]
        public void Focus_and_press_never_change_the_tier()
        {
            // Focus is not a depth change. A raised-versus-flat shadow is
            // exactly the shadow-only, non-textual cue WCAG 1.4.1 excludes,
            // and it is invisible to anyone with reduced contrast
            // sensitivity — which is much of the population that is drawn
            // to a soft-UI theme in the first place. The ring carries
            // focus; the depth carries position.
            Assert.Equal(
                SoftDepthTier.Resting,
                SoftDepthRules.Resolve(
                    SoftDepthTier.Resting, SoftInteractionState.Focused));

            // A press moves the object DOWN into the page, not to a
            // different tier.
            Assert.Equal(
                SoftDepthTier.Raised,
                SoftDepthRules.Resolve(
                    SoftDepthTier.Raised, SoftInteractionState.Pressed));
        }

        // ── radius drives the roll ───────────────────────────────────

        [Fact]
        public void Inset_reach_scales_with_the_corner_radius()
        {
            // The rule that decides whether something looks like clay or
            // like a card with a glow. A large radius with a tight inset
            // produces a hard rim instead of a soft roll.
            var small = Resolve(
                SoftDepthTier.Resting, SoftInteractionState.Rest,
                radius: 16);
            var large = Resolve(
                SoftDepthTier.Resting, SoftInteractionState.Rest,
                radius: 48);

            Assert.True(large.LightStops > small.LightStops,
                "a larger radius must roll further in.");

            // ...and it must stay bounded. Past roughly 60% of the
            // gradient the surface stops reading as inflated and starts
            // reading as fogged.
            Assert.InRange(small.LightStops, 0.12, 0.62);
            Assert.InRange(large.LightStops, 0.12, 0.62);
        }

        [Fact]
        public void Small_surfaces_drop_the_inset_rather_than_smudging()
        {
            // A pill's radius is half its height, so on a 28px chip the
            // inset is asked to resolve inside about 14px of travel. It
            // cannot, and the collapse reads as a dirty rim.
            var stack = Resolve(
                SoftDepthTier.Resting, SoftInteractionState.Rest,
                radius: 14, small: true);

            Assert.Equal(0.0, stack.LightStops);
            Assert.True(stack.HasDrop,
                "a chip keeps its drop — the drop is the whole form.");
        }

        // ── dark mode ────────────────────────────────────────────────

        [Fact]
        public void Dark_mode_recalibrates_the_shadows()
        {
            // Clay is a LIGHT-mode style and inverting the palette
            // produces mud. The two shadows fail for different reasons
            // and need different corrections:
            //
            //   The DROP is tinted to the object's hue — the whole trick
            //   on a pale ground. On a dark ground the same tinted shadow
            //   is indistinguishable from the fill and becomes a smear,
            //   so it must go darker AND far more transparent.
            //
            //   The HIGHLIGHT is the other failure. A bright highlight
            //   that reads as a soft roll on pastel reads as hard plastic
            //   glare on a dark surface. It has to be dimmed.
            var light = Resolve(SoftDepthTier.Resting,
                SoftInteractionState.Rest);
            var dark = Resolve(SoftDepthTier.Resting,
                SoftInteractionState.Rest, dark: true);

            Assert.True(dark.DropOpacity < light.DropOpacity,
                "the dark drop must be more transparent, not stronger.");
            Assert.True(
                dark.DropColor.R + dark.DropColor.G + dark.DropColor.B
                < light.DropColor.R + light.DropColor.G
                  + light.DropColor.B,
                "the dark drop must go darker as well as weaker.");

            Assert.True(dark.LightColor.A < light.LightColor.A,
                "the dark highlight must be dimmed.");
        }

        [Fact]
        public void Dark_mode_carries_depth_in_the_fill_because_shadow_cannot()
        {
            // On a near-black page a shadow cannot separate four depth
            // tiers — every tier collapses into the same flat rectangle.
            // The only lever left is lightness, so each tier raises its
            // fill a few percent instead.
            for (var i = 0; i <= (int)SoftDepthTier.Floating; i++)
            {
                var tier = (SoftDepthTier)i;
                var light = Resolve(tier, SoftInteractionState.Rest);
                var dark = Resolve(tier, SoftInteractionState.Rest,
                    dark: true);

                if (SoftDepthRules.IsConcave(tier)) continue;

                Assert.True(dark.FillLift > 0.0,
                    $"{tier}: dark mode needs a fill lift or the tier is "
                    + "invisible.");
                Assert.True(light.FillLift <= 0.0,
                    $"{tier}: on a light page the shadow IS the signal; "
                    + "lifting the fill too would just wash the tier out.");
            }
        }

        // ── engine rule 4, and the states ────────────────────────────

        [Fact]
        public void Shadows_stay_inside_the_engine_bounds()
        {
            // Rule 4 from the painter: a DropShadowEffect is re-rasterised
            // by the GPU on every repaint, so blur and depth are capped
            // everywhere. Clay is the family that pushes closest to the
            // ceiling because a large radius genuinely wants a large blur,
            // which makes it the one most likely to blow the budget.
            foreach (var dark in new[] { false, true })
            {
                foreach (var tier in new[]
                {
                    SoftDepthTier.Well, SoftDepthTier.Resting,
                    SoftDepthTier.Raised, SoftDepthTier.Floating,
                })
                {
                    foreach (var state in Enum.GetValues<SoftInteractionState>())
                    {
                        var stack = Resolve(tier, state, dark: dark);
                        Assert.InRange(stack.DropBlur, 0, 56);
                        Assert.InRange(stack.DropDepth, 0, 12);
                    }
                }
            }
        }

        [Fact]
        public void A_disabled_surface_loses_its_light_but_keeps_its_form()
        {
            // The drop goes, the highlight goes. If the surface simply
            // stopped being drawn, a disabled control would be
            // unfindable — and in a style whose entire affordance is the
            // shadow, "gone" is indistinguishable from "broken".
            var stack = Resolve(
                SoftDepthTier.Raised, SoftInteractionState.Disabled);

            Assert.False(stack.HasDrop);

            var visuals = ClayVisualFactory.Build(stack, LightSurface);
            Assert.Equal(
                0.0,
                visuals.Light!.GradientStops.First().Color.A / 255.0,
                "a disabled surface must not keep its highlight.");

            // The shade survives so it still reads as a recess in the
            // page rather than a hole punched through it.
            Assert.True(visuals.Shade!.GradientStops
                .Last().Color.A > 0);
        }

        // ── palette plumbing ─────────────────────────────────────────

        [Fact]
        public void Only_the_clay_family_publishes_a_clay_palette()
        {
            // A control still holding a published palette after the user
            // switches to Swiss keeps drawing clay surfaces that no longer
            // match the theme — the failure mode where the theme changed
            // and part of the window did not.
            foreach (var profile in EightThemeCatalog.All)
            {
                ClayPalette.Publish(
                    LightSurface, DarkSurface, ShadeHue, LightHue);

                EightThemePainter.Apply(
                    new ResourceDictionary(), profile,
                    dark: false, accent: ShadeHue);

                bool shouldBeClay =
                    profile.Material.Kind == EightMaterialKind.Inflated;
                Assert.Equal(shouldBeClay, ClayPalette.IsActive);

                if (shouldBeClay)
                {
                    Assert.NotNull(ClayPalette.Light);
                    Assert.NotNull(ClayPalette.Dark);
                }
                else
                {
                    Assert.Null(ClayPalette.Light);
                    Assert.Null(ClayPalette.Dark);
                }
            }

            ClayPalette.Clear();
        }

        [Fact]
        public void Resolving_without_a_published_palette_returns_null()
        {
            // Callers must handle null rather than get a silent fallback:
            // a clay surface that quietly painted itself grey because the
            // palette was missing is a bug that never reproduces on a
            // developer's machine, because there it always works.
            ClayPalette.Clear();
            Assert.Null(ClayPalette.Resolve(
                SoftDepthTier.Resting, SoftInteractionState.Rest,
                radius: 32, dark: false, baseFill: LightSurface));
        }

        private static double Saturation(Color c)
        {
            int max = Math.Max(c.R, Math.Max(c.G, c.B));
            int min = Math.Min(c.R, Math.Min(c.G, c.B));
            return max == 0 ? 0 : (double)(max - min) / max;
        }
    }
}