// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Theme/HoloTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// The family that is most likely to ship beautiful and unusable.
//
// Every other family in this engine has one hard rule that tests can
// pin. Holographic has a harder problem, because its material is
// ANIMATED and its surface is a GRADIENT, and both of those destroy the
// premise contrast checking normally rests on:
//
//   · a screenshot catches one frame of a drifting spectrum
//   · a gradient has no single contrast ratio, only a best point and a
//     worst one, and the glyph lands on whichever physics puts it on
//
// So this suite is built around the worst case rather than the average
// one. The assertions that matter:
//
//   1. THE BANDS ARE NOT UNIFORMLY SPACED. A linear hue sweep reads as
//      a cartoon; this is the single most reliable way to fake the
//      family and it is the first thing checked.
//   2. THE CHROMA CEILING IS RESPECTED. Saturated primaries are inputs;
//      they are not what comes out.
//   3. THE BANDS SHARE ONE LUMINANCE. This is the finding that makes the
//      ink guarantee possible at all, and without it two of the tests
//      below fail on real hardware.
//   4. THE INK IS LEGIBLE ON THE WORST BAND, not merely on the average.
//   5. THE GROUND IS OPAQUE IN EVERY STATE, and no spectral colour ever
//      reaches it.
//   6. REDUCED MOTION FREEZES. It does not speed up.
//
// The recipe is pure arithmetic, so none of this needs a window, a
// dispatcher or a mouse.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using ProGlassAutomation.Services.Theme;
using ProGlassAutomation.Services.Theme.Clay;
using ProGlassAutomation.Services.Theme.Holo;
using ProGlassAutomation.Services.Theme.Neu;
using ProGlassAutomation.Services.Theme.SoftUi;
using ProGlassAutomation.Services.Theme.Swiss;
using Xunit;

namespace ProGlassAutomation.Tests.Theme
{
    public class HoloTests
    {
        // The family profile's real values.
        private static readonly Color LightGround =
            Color.FromRgb(0xFB, 0xF8, 0xFF);
        private static readonly Color LightInk =
            Color.FromRgb(0x17, 0x10, 0x2B);
        private static readonly Color LightMuted =
            Color.FromRgb(0x6A, 0x5F, 0x87);

        private static readonly Color DarkGround =
            Color.FromRgb(0x15, 0x11, 0x28);
        private static readonly Color DarkInk =
            Color.FromRgb(0xF4, 0xF1, 0xFF);
        private static readonly Color DarkMuted =
            Color.FromRgb(0xB5, 0xAC, 0xD8);

        private static readonly Color HolographicAccent =
            Color.FromRgb(0x5B, 0x4B, 0xC4);

        private static HoloProfile Light() =>
            HoloPalette.Build(LightGround, LightInk, LightMuted,
                HolographicAccent, dark: false);

        private static HoloProfile Dark() =>
            HoloPalette.Build(DarkGround, DarkInk, DarkMuted,
                HolographicAccent, dark: true);

        private static HoloStack Resolve(
            bool dark = false,
            SoftInteractionState state = SoftInteractionState.Rest,
            SoftDepthTier tier = SoftDepthTier.Resting,
            bool selected = false,
            bool reducedMotion = false) =>
            new HoloRecipe(dark ? Dark() : Light(), dark, reducedMotion)
                .Resolve(tier, state, selected);

        private static readonly SoftInteractionState[] AllStates =
        {
            SoftInteractionState.Rest,
            SoftInteractionState.Hover,
            SoftInteractionState.Pressed,
            SoftInteractionState.Focused,
            SoftInteractionState.Disabled,
        };

        private static readonly SoftDepthTier[] AllTiers =
        {
            SoftDepthTier.Well,
            SoftDepthTier.Resting,
            SoftDepthTier.Raised,
            SoftDepthTier.Floating,
        };

        // ═══════════════════════════════════════════════════════════
        //  THE GRATING
        //
        //  The failure this guards is a uniform hue sweep. It looks
        //  like a rainbow, which is not what foil looks like.
        // ═══════════════════════════════════════════════════════════

        [Fact]
        public void The_bands_are_NOT_uniformly_spaced()
        {
            foreach (var dark in new[] { false, true })
            {
                var bands = (dark ? Dark() : Light()).Spectrum.Bands;
                Assert.True(bands.Count >= 9,
                    "A spectrum needs many bands to read as foil.");

                var widths = new double[bands.Count - 1];
                for (int i = 0; i < widths.Length; i++)
                    widths[i] = bands[i + 1].Offset - bands[i].Offset;

                double widest = widths.Max();
                double narrowest = widths.Min();
                double ratio = widest / narrowest;

                // Non-uniform is the requirement; BROKEN uniformity is
                // the other failure and is just as wrong. The first
                // version of this recipe used an inverse-square warp
                // that reached a 96x ratio — one rainbow step followed by
                // a 2-degree staircase — and it looked nothing like foil.
                Assert.True(ratio > 1.5,
                    "Bands are evenly spaced, which reads as a rainbow.");

                Assert.True(ratio < 4.0,
                    "Band widths are too uneven to read as a material; "
                    + $"widest/narrowest was {ratio:0.00}.");
            }
        }

        [Fact]
        public void The_warp_redistributes_width_without_changing_coverage()
        {
            // A warp that also moved the total hue range would make
            // every surface drift to a slightly different colour, which
            // is a far more annoying bug than a flat gradient.
            const int n = 13;
            double total = HoloSpectrum.W(n - 1, n);
            double first = HoloSpectrum.W(0, n);

            Assert.Equal(1.0, total, 6);
            Assert.Equal(0.0, first, 6);
        }

        [Fact]
        public void The_spectrum_is_a_single_pass_not_a_rainbow_repeated()
        {
            // The ramp's own borders already lead with #FF0080, so the
            // generated spectrum has to start in the same place or the
            // painted tokens and the computed foil disagree at the one
            // edge the eye checks first.
            foreach (var dark in new[] { false, true })
            {
                var first = (dark ? Dark() : Light()).Spectrum.Bands[0];
                Assert.InRange(first.Hue, 300.0, 345.0);
            }
        }

        [Fact]
        public void No_band_repeats_and_no_band_is_its_own_halfway_partner()
        {
            // A repeat at exactly halfway is the seam a viewer notices
            // as "this gradient is tiled" rather than as foil.
            foreach (var dark in new[] { false, true })
            {
                var bands = (dark ? Dark() : Light()).Spectrum.Bands;
                int half = bands.Count / 2;

                Assert.Equal(bands.Count, bands.Select(b => b.Color).Distinct().Count());

                for (int i = 0; i + half < bands.Count; i++)
                    Assert.NotEqual(bands[i].Color, bands[i + half].Color);
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  THE CHROMA CEILING
        // ═══════════════════════════════════════════════════════════

        [Fact]
        public void Every_band_respects_the_chroma_ceiling()
        {
            foreach (var dark in new[] { false, true })
            {
                var profile = dark ? Dark() : Light();

                Assert.Equal(0.55, profile.ChromaCeiling, 6);
                Assert.All(profile.Spectrum.Bands,
                    b => Assert.True(b.Chroma <= profile.ChromaCeiling,
                        $"band at hue {b.Hue:0} had chroma {b.Chroma:0.000}, "
                        + "over the ceiling."));
            }
        }

        [Fact]
        public void Chroma_varies_across_the_spectrum()
        {
            // A spectrum at one constant chroma is the second most
            // common tell: real foil's bands differ in VIVACITY as well
            // as hue, because a band at a grazing angle returns less
            // light than one head-on.
            var chromas = Light().Spectrum.Bands
                .Select(b => b.Chroma).ToArray();

            Assert.True(chromas.Max() - chromas.Min() > 0.05,
                "Every band has the same chroma, which reads as a "
                + "printer's colour wheel rather than as foil.");
        }

        // ═══════════════════════════════════════════════════════════
        //  ONE LUMINANCE FOR THE WHOLE SPECTRUM
        //
        //  This is the finding that makes the ink guarantee possible.
        //  At a constant HSL lightness a spectrum's WCAG relative
        //  luminance spans roughly ten to one — yellow-green near 0.6,
        //  deep blue near 0.06 — so no single ink can clear 4.5:1
        //  across it. Measured on the pre-fix spectrum: the light ink
        //  scored 1.49:1 on the yellow-green band.
        // ═══════════════════════════════════════════════════════════

        [Fact]
        public void Every_band_shares_one_luminance()
        {
            foreach (var dark in new[] { false, true })
            {
                var bands = (dark ? Dark() : Light()).Spectrum.Bands;
                var lums = bands.Select(b => Luminance(b.Color)).ToArray();

                double lo = lums.Min();
                double hi = lums.Max();

                // The tolerance is 0.005 and that is not slack: the
                // result is an 8-bit RGB colour, so achievable
                // luminances are a discrete lattice roughly 0.003 apart
                // in the mid-tones. Sixteen bisection steps converge far
                // below that. The margin over a 4.5:1 floor is about two
                // whole contrast points, so the residue cannot matter.
                Assert.True(hi - lo < 0.005,
                    $"Band luminance spread {hi - lo:0.0000} — the "
                    + "spectrum is not flat enough for one ink to serve "
                    + "the whole of it.");
            }
        }

        [Fact]
        public void The_two_modes_target_opposite_luminances()
        {
            // Light mode's bands are BRIGHT because its ink is dark;
            // dark mode's are DARK because its ink is light. One target
            // luminance cannot serve both, which is why this is a
            // per-mode constant rather than a single number.
            Assert.True(HoloPalette.LightBandLuminance
                > HoloPalette.DarkBandLuminance);

            Assert.True(
                Luminance(Light().Spectrum.Bands[0].Color)
                    > Luminance(Dark().Spectrum.Bands[0].Color),
                "Light mode's foil should be the brighter of the two.");
        }

        // ═══════════════════════════════════════════════════════════
        //  THE INK GUARANTEE — THE WORST BAND, NOT THE AVERAGE
        // ═══════════════════════════════════════════════════════════

        [Fact]
        public void The_family_ink_is_legible_on_the_WORST_band()
        {
            foreach (var dark in new[] { false, true })
            {
                var recipe = new HoloRecipe(
                    dark ? Dark() : Light(), dark, false);

                double worst = recipe.WorstBandContrast(
                    (dark ? Dark() : Light()).Ink);

                // Measured 6.76:1 light and 6.22:1 dark. The assertion
                // is at AA because that is the promise; the reason it
                // lands so far above is the constant-luminance solve.
                Assert.True(worst >= HoloPalette.MinimumBodyContrast,
                    $"{(dark ? "dark" : "light")} worst-band contrast was "
                    + $"{worst:0.00}:1.");
            }
        }

        [Fact]
        public void ChipInk_repairs_an_ink_that_cannot_survive_the_foil()
        {
            // Every one of these is a plausible thing for a host to
            // pass, and every one of them is unreadable on at least one
            // band before the repair.
            var hostile = new[]
            {
                Colors.White,
                Colors.Black,
                Color.FromRgb(0x80, 0x80, 0x80),
                Color.FromRgb(0x00, 0x00, 0xFF),
                Color.FromRgb(0xFF, 0xFF, 0x00),
                Color.FromRgb(0x0A, 0xC8, 0x0A),
            };

            foreach (var dark in new[] { false, true })
            {
                var profile = dark ? Dark() : Light();
                var recipe = new HoloRecipe(profile, dark, false);

                foreach (var ink in hostile)
                {
                    double before = recipe.WorstBandContrast(ink);
                    var fixedInk = recipe.ChipInk(ink);
                    double after = recipe.WorstBandContrast(fixedInk);

                    Assert.True(after >= HoloPalette.MinimumBodyContrast,
                        $"{(dark ? "dark" : "light")}: #"
                        + $"{ink.R:X2}{ink.G:X2}{ink.B:X2} went from "
                        + $"{before:0.00}:1 to {after:0.00}:1, still "
                        + "unreadable on its worst band.");
                }
            }
        }

        [Fact]
        public void ChipInk_leaves_a_compliant_ink_exactly_alone()
        {
            // Otherwise the repair would quietly restyle every host
            // colour that was already fine.
            foreach (var dark in new[] { false, true })
            {
                var profile = dark ? Dark() : Light();
                var recipe = new HoloRecipe(profile, dark, false);

                Assert.Equal(profile.Ink, recipe.ChipInk(profile.Ink));
            }
        }

        [Fact]
        public void ChipInk_only_ever_improves_the_worst_band()
        {
            foreach (var dark in new[] { false, true })
            {
                var recipe = new HoloRecipe(
                    dark ? Dark() : Light(), dark, false);

                foreach (var ink in new[]
                {
                    Colors.White, Colors.Black,
                    Color.FromRgb(0x80, 0x80, 0x80),
                    Color.FromRgb(0x00, 0x00, 0xFF),
                })
                {
                    double before = recipe.WorstBandContrast(ink);
                    double after = recipe.WorstBandContrast(
                        recipe.ChipInk(ink));

                    Assert.True(after >= before,
                        "The repair made a colour less readable.");
                }
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  THE GROUND IS OPAQUE, ALWAYS
        // ═══════════════════════════════════════════════════════════

        [Fact]
        public void The_ground_is_opaque_in_every_state_and_tier()
        {
            foreach (var dark in new[] { false, true })
            {
                foreach (var tier in AllTiers)
                {
                    foreach (var state in AllStates)
                    {
                        var stack = Resolve(dark, state, tier);

                        Assert.Equal(255, stack.Ground.A);
                        Assert.Equal(255, stack.Ink.A);
                        Assert.Equal(255, stack.MutedInk.A);
                    }
                }
            }
        }

        [Fact]
        public void A_translucent_ground_is_forced_opaque_at_build_time()
        {
            // The ramp could hand this family a translucent surface. It
            // is forced to alpha 255 here rather than trusted, because an
            // alpha on the ground is precisely the thing that would put a
            // hue behind a label — and there is no later repair for it
            // that does not involve guessing a colour.
            var profile = HoloPalette.Build(
                Color.FromArgb(0x80, 0x20, 0x20, 0x40),
                LightInk, LightMuted, HolographicAccent, dark: false);

            Assert.Equal(255, profile.Ground.A);
        }

        [Fact]
        public void No_spectral_colour_ever_reaches_the_ground_or_the_ink()
        {
            // The structural promise, checked rather than trusted: the
            // foil and the reading surface are separate colour sets and
            // no state mixes them.
            foreach (var dark in new[] { false, true })
            {
                var rim = (dark ? Dark() : Light()).Spectrum.Bands
                    .Select(b => b.Color).ToHashSet();

                foreach (var tier in AllTiers)
                {
                    foreach (var state in AllStates)
                    {
                        var stack = Resolve(dark, state, tier);

                        Assert.False(rim.Contains(stack.Ground),
                            "A spectral colour landed on the ground.");
                        Assert.False(rim.Contains(stack.Ink),
                            "A spectral colour landed on the ink.");
                        Assert.False(stack.HasFoilUnderText);
                    }
                }
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  INTERACTION LIVES ON THE RIM
        // ═══════════════════════════════════════════════════════════

        [Fact]
        public void Interaction_raises_the_rim_and_leaves_the_ground_alone()
        {
            // This is the family's whole interaction vocabulary, and it
            // is what keeps a label's contrast constant while the foil
            // drifts underneath it.
            var rest = Resolve();

            foreach (var state in new[]
            {
                SoftInteractionState.Hover,
                SoftInteractionState.Pressed,
                SoftInteractionState.Focused,
            })
            {
                var stack = Resolve(state: state);

                Assert.Equal(rest.Ground, stack.Ground);
                Assert.Equal(rest.Ink, stack.Ink);
                Assert.True(stack.RimOpacity > rest.RimOpacity,
                    $"{state} should brighten the rim.");
            }
        }

        [Fact]
        public void Press_never_changes_the_colour_under_the_label()
        {
            // Every other family signals a press by changing the surface
            // under the text. Holographic deliberately does not: the
            // label would change contrast on the exact frame it was
            // clicked, which is the worst possible moment to lose it.
            var rest = Resolve();
            var press = Resolve(state: SoftInteractionState.Pressed);

            Assert.True(press.RimOpacity > rest.RimOpacity);
            Assert.Equal(rest.Ground, press.Ground);
            Assert.Equal(rest.Ink, press.Ink);
            Assert.Equal(rest.MutedInk, press.MutedInk);
        }

        [Fact]
        public void Disabled_fades_the_foil_rather_than_the_ground()
        {
            // A disabled control that keeps a bright spectral rim is the
            // brightest thing on the screen, which is the opposite of
            // what disabled means — and a glowing edge is exactly what a
            // user reads as "this one is live".
            var rest = Resolve();
            var off = Resolve(state: SoftInteractionState.Disabled);

            Assert.True(off.RimOpacity < rest.RimOpacity);
            Assert.Equal(rest.Ground, off.Ground);
        }

        [Fact]
        public void Rim_opacity_never_leaves_the_zero_to_one_range()
        {
            foreach (var dark in new[] { false, true })
            {
                foreach (var state in AllStates)
                {
                    var stack = Resolve(dark, state);
                    Assert.InRange(stack.RimOpacity, 0.0, 1.0);
                }
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  MOTION
        // ═══════════════════════════════════════════════════════════

        [Fact]
        public void The_two_motion_rates_are_never_lock_stepped()
        {
            // Two animations on the same clock is the mechanical tell:
            // it reads as a CSS loop rather than as a material, because
            // real foil responds to TILT and to spectral diffraction
            // rate, which are not the same clock.
            var m = Light().Motion;

            Assert.NotEqual(m.DriftPeriod, m.SweepPeriod);
            Assert.NotEqual(
                m.DriftPeriod % m.SweepPeriod, 0.0,
                "One period divides the other exactly, which is "
                + "lock-step with extra steps.");
        }

        [Fact]
        public void The_motion_is_glacial_rather_than_traceable()
        {
            // "Animate at glacial speed. If a user can consciously track
            // the movement, it's too fast. Think 20-60 second loops
            // minimum." The foil's drift is the background spectrum and
            // obeys that. The sheen is a specular sweep across a 1px
            // rim, so it runs faster — and its opacity is low enough
            // (0.42) that it does not read as content.
            var m = Light().Motion;

            Assert.True(m.DriftPeriod >= 20000.0,
                "The foil drift is fast enough to track.");

            Assert.True(m.SheenOpacity <= 0.5,
                "The sheen is bright enough to be mistaken for content.");

            Assert.True(m.SheenWidth is > 0.05 and <= 0.9,
                "A sheen narrower than 5% vanishes; wider than 90% "
                + "reads as chrome rather than as foil.");
        }

        [Fact]
        public void The_grating_axis_comes_from_the_shared_light()
        {
            // Every other family already obeys SoftLight. A foil that
            // swept on its own axis would be the one lit object in the
            // app pointing somewhere else.
            var m = Light().Motion;
            Assert.Equal(SoftLight.HighlightDirection, m.RimAngle, 6);
        }

        [Fact]
        public void Reduced_motion_freezes_the_foil_rather_than_speeding_it_up()
        {
            // WCAG 2.3.3 is about the vestibular response, and a 26s
            // loop sped up to 2s produces that response perfectly well.
            // Reduced motion has to mean STOP.
            foreach (var dark in new[] { false, true })
            {
                var moving = Resolve(dark, reducedMotion: false);
                var frozen = Resolve(dark, reducedMotion: true);

                Assert.True(moving.IsAnimated);
                Assert.False(frozen.IsAnimated);
                Assert.True(frozen.MotionReduced);

                // Freezing changes nothing about the CONSTRUCTION —
                // same ground, same ink, same rim. Only whether it moves
                // is affected, so a user who freezes the foil does not
                // also lose the material.
                Assert.Equal(moving.Ground, frozen.Ground);
                Assert.Equal(moving.Ink, frozen.Ink);
                Assert.Equal(moving.RimOpacity, frozen.RimOpacity);
                Assert.Equal(moving.Rim.Count, frozen.Rim.Count);
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  GEOMETRY
        // ═══════════════════════════════════════════════════════════

        [Fact]
        public void Larger_surfaces_get_larger_corners()
        {
            // Unlike Swiss, where the style abolishes the corner, and
            // unlike neumorphism, where the radius IS the construction:
            // the foil is a film ON something, so the something keeps
            // its own silhouette. Flat at zero would read as a sticker.
            var recipe = new HoloRecipe(Light(), false, false);

            double well = recipe.RadiusFor(SoftDepthTier.Well);
            double resting = recipe.RadiusFor(SoftDepthTier.Resting);
            double floating = recipe.RadiusFor(SoftDepthTier.Floating);

            Assert.True(well < resting);
            Assert.True(resting < floating);
            Assert.All(AllTiers, t => Assert.True(recipe.RadiusFor(t) > 0.0));
        }

        [Fact]
        public void The_chip_ink_shield_is_fully_opaque()
        {
            // The shield is what makes the foil legal under a glyph. If
            // this were ever anything below 1.0 the guarantee would be a
            // claim rather than a fact.
            foreach (var dark in new[] { false, true })
            {
                foreach (var state in AllStates)
                {
                    Assert.Equal(
                        1.0, Resolve(dark, state).AccentInkAlpha);
                }
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  PALETTE BRIDGE
        // ═══════════════════════════════════════════════════════════

        [Fact]
        public void Only_the_holographic_family_publishes_a_holo_palette()
        {
            foreach (var profile in EightThemeCatalog.All)
            {
                HoloPalette.Publish(
                    LightGround, LightInk, LightMuted, HolographicAccent,
                    DarkGround, DarkInk, DarkMuted, HolographicAccent);

                EightThemePainter.Apply(
                    new ResourceDictionary(), profile,
                    dark: false, accent: Colors.SteelBlue);

                bool shouldBeHolo =
                    profile.Material.Kind == EightMaterialKind.Foil;
                Assert.Equal(shouldBeHolo, HoloPalette.IsActive);

                if (shouldBeHolo)
                {
                    Assert.NotNull(HoloPalette.Light);
                    Assert.NotNull(HoloPalette.Dark);
                }
                else
                {
                    Assert.Null(HoloPalette.Light);
                    Assert.Null(HoloPalette.Dark);
                }
            }

            HoloPalette.Clear();
        }

        [Fact]
        public void No_two_of_the_four_control_palettes_are_active_at_once()
        {
            // A control holding another family's palette renders the
            // wrong construction under a theme it does not belong to,
            // and it looks exactly like a theme change that
            // half-happened. For this family the symptom is worse than
            // cosmetic: a stale foil keeps DRIFTING over a neumorphic
            // surface forever.
            foreach (var profile in EightThemeCatalog.All)
            {
                EightThemePainter.Apply(
                    new ResourceDictionary(), profile,
                    dark: false, accent: Colors.SteelBlue);

                int active = 0;
                if (ClayPalette.IsActive) active++;
                if (NeuPalette.IsActive) active++;
                if (SwissPalette.IsActive) active++;
                if (HoloPalette.IsActive) active++;

                Assert.True(active <= 1,
                    $"{profile.Identity}: {active} control palettes were "
                    + "published at once.");
            }

            HoloPalette.Clear();
            SwissPalette.Clear();
            ClayPalette.Clear();
            NeuPalette.Clear();
        }

        [Fact]
        public void Publishing_and_clearing_are_idempotent()
        {
            HoloPalette.Clear();
            Assert.False(HoloPalette.IsActive);

            int raised = 0;
            void Count(object? s, EventArgs e) => raised++;

            // Clear() on an already-clear palette must not raise Changed,
            // because controls repaint on that event and a spurious
            // raise repaints the whole visual tree for nothing.
            HoloPalette.Changed += Count;
            HoloPalette.Clear();
            Assert.Equal(0, raised);

            HoloPalette.Publish(
                LightGround, LightInk, LightMuted, HolographicAccent,
                DarkGround, DarkInk, DarkMuted, HolographicAccent);
            Assert.Equal(1, raised);
            Assert.True(HoloPalette.IsActive);

            HoloPalette.Clear();
            Assert.Equal(2, raised);
            HoloPalette.Changed -= Count;
        }

        // ── helpers ──────────────────────────────────────────────────

        private static double Luminance(Color c)
        {
            static double Channel(byte v)
            {
                double s = v / 255.0;
                return s <= 0.04045
                    ? s / 12.92
                    : Math.Pow((s + 0.055) / 1.055, 2.4);
            }

            return 0.2126 * Channel(c.R)
                 + 0.7152 * Channel(c.G)
                 + 0.0722 * Channel(c.B);
        }

        private static double Contrast(Color a, Color b)
        {
            double la = Luminance(a);
            double lb = Luminance(b);

            if (la < lb) (la, lb) = (lb, la);
            return (la + 0.05) / (lb + 0.05);
        }
    }
}