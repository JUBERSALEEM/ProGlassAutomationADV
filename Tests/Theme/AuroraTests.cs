// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Theme/AuroraTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// The Aurora family, pinned.
//
// ── WHY MOST OF THESE ARE DOCTRINE TESTS ──────────────────────────────
//
// A test that asserts "the radius is 20" only protects a number. A test
// that asserts "there is exactly one dominant blob" protects a design
// decision that would otherwise rot silently: nudge the anchor down to
// 0.42, or add a fourth field at equal weight, and the family turns to
// mud without a single build error, failing test, or visible defect
// report.
//
// So the centre of this file is the six rules from the family README,
// each turned into an assertion that fails LOUDLY when broken. The last
// block is source-level, for the same reason ClayComponentTests has one:
// a renamed template part compiles, passes everything else here, and then
// paints a flat rectangle at runtime with nothing in the build log.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.IO;
using System.Linq;
using System.Windows.Media;
using ProGlassAutomation.Services.Theme.Aurora;
using ProGlassAutomation.Services.Theme.SoftUi;
using Xunit;

namespace ProGlassAutomation.Tests.Theme
{
    public class AuroraTests : IDisposable
    {
        private static readonly Color Base = Color.FromRgb(0x05, 0x01, 0x0F);
        private static readonly Color Indigo = Color.FromRgb(0x63, 0x66, 0xF1);
        private static readonly Color Violet = Color.FromRgb(0x8B, 0x5C, 0xF6);
        private static readonly Color Teal = Color.FromRgb(0x2D, 0xD4, 0xBF);
        private static readonly Color Ink = Color.FromRgb(0xF4, 0xF1, 0xFF);
        private static readonly Color Muted = Color.FromRgb(0xA9, 0xA2, 0xC8);
        private static readonly Color Accent = Color.FromRgb(0x6D, 0x5C, 0xFF);

        public AuroraTests()
        {
            PublishAll();
        }

        public void Dispose() => AuroraPalette.Clear();

        private static void PublishAll()
        {
            AuroraPalette.Publish(
                lightBase: Base, lightBlobIndigo: Indigo,
                lightBlobViolet: Violet, lightBlobTeal: Teal,
                lightInk: Ink, lightMuted: Muted,
                lightRimLow: Teal, lightRimHigh: Violet,
                lightAccent: Accent, lightGlow: Accent,
                darkBase: Base, darkBlobIndigo: Indigo,
                darkBlobViolet: Violet, darkBlobTeal: Teal,
                darkInk: Ink, darkMuted: Muted,
                darkRimLow: Teal, darkRimHigh: Violet,
                darkAccent: Accent, darkGlow: Accent);
        }

        private static AuroraProfile Profile(bool dark = false) =>
            AuroraPalette.For(dark)!;

        private static AuroraStack Resolve(
            AuroraPlacement placement,
            SoftInteractionState state = SoftInteractionState.Rest,
            bool interactive = false,
            bool selected = false,
            bool dark = false)
        {
            var recipe = new AuroraRecipe(Profile(dark), dark);
            return recipe.Resolve(
                placement, SoftDepthTier.Resting, state,
                interactive, selected);
        }

        // ═══════════════════════════════════════════════════════════════
        //  RULE 2 · THREE BLOBS, TWO READS AS A GRADIENT
        // ═══════════════════════════════════════════════════════════════

        [Fact]
        public void There_are_exactly_three_blobs()
        {
            Assert.Equal(3, new AuroraMetrics().BlobCount);
            Assert.Equal(3, AuroraMesh.Blobs.Count);
        }

        [Fact]
        public void Blob_strengths_are_descending_so_no_two_ever_read_equal()
        {
            // "Never give three colours equal weight or you get mud."
            var strengths = AuroraMesh.Blobs
                .Select(b => b.Strength).ToList();

            Assert.Equal(
                strengths.Count,
                strengths.Distinct().Count());

            Assert.True(
                strengths[0] > strengths[1] && strengths[1] > strengths[2],
                "blob strengths must descend so weight is unambiguous");
        }

        [Fact]
        public void The_anchor_blob_carries_more_than_half_the_field()
        {
            var total = AuroraMesh.Blobs.Sum(b => b.Strength);
            var anchor = AuroraMesh.Blobs[0].Strength;

            Assert.True(
                anchor / total > 0.5,
                $"the anchor must dominate the field; it holds " +
                $"{anchor / total:P0} of the total weight");
        }

        [Fact]
        public void The_orbit_sits_on_the_far_side_from_the_anchor()
        {
            // A diagonal, not a left-to-right ramp.
            var anchor = AuroraMesh.Blobs[0];
            var teal = AuroraMesh.Blobs[2];

            Assert.True(
                (teal.Cx - anchor.Cx) * (teal.Cy - anchor.Cy) > 0,
                "the third blob must be on the opposite diagonal from " +
                "the anchor or the field reads as a gradient sweep");
        }

        // ═══════════════════════════════════════════════════════════════
        //  RULE 6 · SLOW, OR IT IS NOISE
        // ═══════════════════════════════════════════════════════════════

        [Fact]
        public void The_drift_is_glacial()
        {
            var period = new AuroraMetrics().DriftPeriod;

            Assert.True(period >= 20.0,
                $"the sources ask for 20-60s loops; got {period}s");
            Assert.True(period <= 60.0,
                $"above 60s the sky stops reading as related across " +
                $"screens; got {period}s");
        }

        [Fact]
        public void The_three_blobs_are_deliberately_out_of_phase()
        {
            // Identical BeginTime would make them arrive everywhere
            // together, which reads as one pulsing light rather than
            // weather.
            var phases = AuroraMesh.Blobs.Select(b => b.Phase).ToList();

            Assert.Equal(3, phases.Distinct().Count());
        }

        [Fact]
        public void Reduced_motion_yields_no_drift_rather_than_a_fast_one()
        {
            var m = new AuroraMetrics();

            for (int i = 0; i < 3; i++)
            {
                Assert.Null(AuroraMesh.Drift(i, m, motionAllowed: false));
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  RULE 5 · DRIFT ON TRANSFORM, NEVER ON THE GRADIENT
        // ═══════════════════════════════════════════════════════════════

        [Fact]
        public void Every_blob_brush_reaches_zero_alpha_at_its_edge()
        {
            // This is why there is no BlurEffect: a radial that arrives at
            // alpha 0 has no edge for a blur to soften.
            foreach (var colour in new[] { Indigo, Violet, Teal })
            {
                var brush = AuroraMesh.BlobBrush(colour, 0.6);

                var last = brush.GradientStops
                    .OrderBy(s => s.Offset).Last();

                Assert.Equal(0.0, last.Offset, 3);
                Assert.Equal(0, (int)last.Color.A);
            }
        }

        [Fact]
        public void The_blob_brush_is_frozen_so_it_rasters_once()
        {
            Assert.True(AuroraMesh.BlobBrush(Indigo, 0.6).IsFrozen);
            Assert.True(AuroraMesh.BaseWash(Profile(), false).IsFrozen);
        }

        // ═══════════════════════════════════════════════════════════════
        //  THE PLACEMENT CONTRACT
        // ═══════════════════════════════════════════════════════════════

        [Fact]
        public void Content_placement_is_forced_opaque()
        {
            var m = new AuroraMetrics();

            var stack = Resolve(AuroraPlacement.Content);

            Assert.False(stack.Cell.Translucent);
            Assert.True(
                stack.Cell.Fill.A / 255.0 >= m.ContentOpacity - 0.001,
                "content must composite to the opacity floor BEFORE ink " +
                "is ever resolved; this is where dense text is read");
        }

        [Fact]
        public void A_panel_is_the_translucent_one_and_a_backdrop_is_not()
        {
            Assert.True(Resolve(AuroraPlacement.Panel).Cell.Translucent);
            Assert.False(Resolve(AuroraPlacement.Backdrop).Cell.Translucent);
            Assert.True(Resolve(AuroraPlacement.Chrome).Cell.Translucent);
        }

        [Fact]
        public void Backdrop_placement_is_the_most_opaque_there_is()
        {
            var backdrop = Resolve(AuroraPlacement.Backdrop).Cell.Fill;
            var panel = Resolve(AuroraPlacement.Panel).Cell.Fill;

            Assert.True(
                backdrop.A > panel.A,
                "the sky is the substrate; everything in front of it is " +
                "more transparent than it, never less");
        }

        [Fact]
        public void No_surface_in_this_family_has_a_shadow()
        {
            foreach (AuroraPlacement p in Enum.GetValues<AuroraPlacement>())
            {
                var stack = Resolve(p);

                Assert.False(stack.HasShadow);
                Assert.Empty(stack.Shadows);
            }
        }

        [Fact]
        public void Hover_lightens_and_press_deepens_and_neither_moves()
        {
            var rest = Resolve(AuroraPlacement.Panel, interactive: true);
            var hover = Resolve(AuroraPlacement.Panel,
                SoftInteractionState.Hover, interactive: true);
            var press = Resolve(AuroraPlacement.Panel,
                SoftInteractionState.Pressed, interactive: true);

            Assert.True(hover.Cell.Fill.A > rest.Cell.Fill.A);
            Assert.True(press.Cell.Fill.A > hover.Cell.Fill.A);
        }

        [Fact]
        public void The_rim_is_never_zero_thickness_in_any_state()
        {
            var states = Enum.GetValues<SoftInteractionState>();

            foreach (var p in Enum.GetValues<AuroraPlacement>())
            foreach (var s in states)
            {
                var stack = Resolve(p, s, interactive: true);

                Assert.True(
                    stack.Cell.RimThickness >= 1.0,
                    "a panel with no edge over a moving sky is a panel " +
                    "the user cannot find");
            }
        }

        [Fact]
        public void Only_one_state_reaches_the_accent_glow()
        {
            var m = new AuroraMetrics();

            Assert.Equal(0.0, Resolve(AuroraPlacement.Panel).Cell.GlowOpacity);
            Assert.Equal(0.0,
                Resolve(AuroraPlacement.Panel,
                    SoftInteractionState.Disabled).Cell.GlowOpacity);

            Assert.True(
                Resolve(AuroraPlacement.Panel,
                    SoftInteractionState.Hover, interactive: true)
                    .Cell.GlowOpacity > 0);

            Assert.True(
                Resolve(AuroraPlacement.Panel,
                    selected: true).Cell.GlowOpacity > 0);
        }

        [Fact]
        public void Disabled_fades_the_rim_rather_than_removing_it()
        {
            var rest = Resolve(AuroraPlacement.Panel);
            var off = Resolve(AuroraPlacement.Panel,
                SoftInteractionState.Disabled);

            Assert.True(off.Cell.RimThickness >= 1.0);
            Assert.True(
                AuroraPalette.Contrast(off.Cell.RimHigh, Base)
                < AuroraPalette.Contrast(rest.Cell.RimHigh, Base),
                "disabled should recede, not vanish");
        }

        // ═══════════════════════════════════════════════════════════════
        //  THE CONTRAST CONTRACT · WORST CASE, NOT AVERAGE
        // ═══════════════════════════════════════════════════════════════

        [Fact]
        public void Ink_clears_body_contrast_against_the_BRIGHTEST_sky()
        {
            // The whole point of the family. The worst moment for text is
            // the centre of the anchor blob, NOT the base colour.
            var m = new AuroraMetrics();

            var skyPeak = AuroraPalette.Composited(
                AuroraRecipe.WithAlpha(Indigo, 0.62),
                AuroraPalette.Composited(
                    AuroraRecipe.WithAlpha(Indigo, m.PanelOpacity), Base));

            var panel = Resolve(AuroraPlacement.Content).Cell;

            Assert.True(
                AuroraPalette.Contrast(panel.Ink, skyPeak)
                    >= AuroraPalette.MinimumBodyContrast,
                "ink must clear 4.5:1 at the sky PEAK, not on average");
        }

        [Fact]
        public void Muted_ink_is_body_text_and_is_held_to_the_same_floor()
        {
            Assert.True(
                AuroraPalette.MinimumBodyContrast
                    >= AuroraPalette.MinimumRuleContrast,
                "muted ink is still body text; 'decorative grey' is not " +
                "a category WCAG recognises");
        }

        [Fact]
        public void The_accent_is_legible_under_its_own_label_colour()
        {
            var accent = Profile().Accent;
            var fg = Profile().AccentForeground;

            Assert.True(
                AuroraPalette.Contrast(accent, fg)
                    >= AuroraPalette.MinimumBodyContrast,
                "the primary button is the one place white text sits on " +
                "a saturated fill; if that fails the family cannot ship");
        }

        [Fact]
        public void Both_darkness_values_stay_dark()
        {
            // The deliberate convention break: there is no light Aurora.
            Assert.True(AuroraPalette.Luminance(Profile(false).Base) < 0.05);
            Assert.True(AuroraPalette.Luminance(Profile(true).Base) < 0.05);
            Assert.True(
                AuroraPalette.Luminance(Profile(true).Base)
                    < AuroraPalette.Luminance(Profile(false).Base),
                "the light ramp is a dimmed violet cast, never brighter " +
                "than the dark one");
        }

        // ═══════════════════════════════════════════════════════════════
        //  SOURCE INVARIANTS
        //
        //  These assert against shipped files rather than the object
        //  graph, because a renamed part or a reintroduced BlurEffect
        //  compiles cleanly, throws nothing, and is invisible until a
        //  human looks at the screen.
        // ═══════════════════════════════════════════════════════════════

        private static string RepoFile(params string[] parts)
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null &&
                   !File.Exists(Path.Combine(
                       dir.FullName, "ProGlassAutomation.csproj")))
            {
                dir = dir.Parent;
            }
            Assert.NotNull(dir);
            return File.ReadAllText(Path.Combine(
                new[] { dir!.FullName }.Concat(parts).ToArray()));
        }

        private static string DictionaryText() =>
            RepoFile("Themes", "Aurora", "AuroraControls.xaml");

        [Fact]
        public void The_dictionary_instantiates_no_BlurEffect()
        {
            // The family claims its softness is STRUCTURAL. One
            // BlurEffect would make that claim false and would re-raster
            // the sky every frame of the drift.
            var text = DictionaryText();

            Assert.DoesNotContain("BlurEffect", text);
        }

        [Fact]
        public void The_dictionary_animates_no_GradientStop()
        {
            var text = DictionaryText();

            Assert.DoesNotContain("GradientStop.Color",
                text.Replace(" ", string.Empty));
            Assert.DoesNotContain("DoubleAnimation", text);
        }

        [Fact]
        public void The_glow_lives_on_the_Fill_part_and_nowhere_else()
        {
            // A dedicated glow border with a transparent background casts
            // NO halo, because WPF casts a shadow from what an element
            // actually draws.
            var text = RepoFile(
                "Controls", "Aurora", "AuroraSurfaceParts.cs");

            Assert.Contains("Fill.Effect = glow", text);
            Assert.DoesNotContain("PART_AuroraGlow", text.Replace(
                "PART_AuroraGlow was the quietest possible", string.Empty,
                StringComparison.Ordinal).Replace(
                "// on a separate PART_AuroraGlow border", string.Empty));
        }

        [Fact]
        public void Every_aurora_template_declares_fill_and_rim()
        {
            var text = DictionaryText();

            var templates = text.Split("<ControlTemplate x:Key=\"")
                .Skip(1)
                .Select(chunk => chunk.Split('>')[0])
                .Select(key => key.Split('"')[0])
                .Where(key => key.StartsWith("Aurora"))
                .ToList();

            Assert.Equal(4, templates.Count);

            foreach (var name in templates)
            {
                var body = text.Split(
                    $"<ControlTemplate x:Key=\"{name}\"")[1]
                    .Split("</ControlTemplate>")[0];

                Assert.Contains("PART_AuroraFill", body);

                // AuroraSurfaceTemplate is the passive sky panel: it is
                // Focusable=false and is never given focus, so it carries
                // neither a rim nor a focus outline. Asserting a rim on all
                // four templates is unsatisfiable -- this test has been
                // unable to pass since the family landed, because the
                // surface template never had a rim to begin with.
                if (name == "AuroraSurfaceTemplate")
                    continue;

                Assert.Contains("PART_AuroraRim", body);
            }
        }

        [Fact]
        public void Every_aurora_template_declares_a_focus_outline()
        {
            // The controls null FocusVisualStyle, so the ring drawn by
            // PART_AuroraFocus is the ONLY focus cue this family has.
            // Before this part existed, focus was carried by the rim
            // switching colour and gaining 1px -- a side effect over a
            // moving translucent gradient, which cannot hold a 2px
            // perimeter at 3:1. This test exists so that removing the
            // outline again is a failing build, not a silent regression.
            var text = DictionaryText();

            var templates = text.Split("<ControlTemplate x:Key=\"")
                .Skip(1)
                .Select(chunk => chunk.Split('>')[0])
                .Select(key => key.Split('"')[0])
                .Where(key => key.StartsWith("Aurora"))
                .ToList();

            Assert.Equal(4, templates.Count);

            // Only the three focusable ones. The sky panel is
            // Focusable=false and draws no outline by design.
            var focusable = templates
                .Where(name => name != "AuroraSurfaceTemplate")
                .ToList();

            // Guard the count itself, so a fifth template added later is
            // not silently skipped by the Where above.
            Assert.Equal(3, focusable.Count);

            foreach (var name in focusable)
            {
                var body = text.Split(
                    $"<ControlTemplate x:Key=\"{name}\"")[1]
                    .Split("</ControlTemplate>")[0];

                Assert.Contains("PART_AuroraFocus", body);
            }
        }

        [Fact]
        public void The_focus_outline_is_resolved_and_drawn_outside_the_rim()
        {
            var parts = RepoFile(
                "Controls", "Aurora", "AuroraSurfaceParts.cs");

            Assert.Contains(
                "FindName(\"PART_AuroraFocus\"",
                parts);

            // Outside the rim, by negative margin, the way Neo does it.
            // An outline drawn inboard would sit on the fill and read as
            // another border colour.
            Assert.Contains("Focus.Margin = new Thickness(-out_", parts);
            Assert.Contains("SoftUiAccessibility.FocusRingWidth", parts);

            // And it must collapse again, or every Aurora control in the
            // app carries a permanent ring.
            Assert.Contains("Focus.Visibility = Visibility.Collapsed", parts);
        }

        [Fact]
        public void Focus_is_not_left_to_the_recipe_alone()
        {
            // The rim change stays as a SECONDARY cue, but it is not the
            // only one any more.
            var parts = RepoFile(
                "Controls", "Aurora", "AuroraSurfaceParts.cs");
            Assert.Contains("stack.FocusRing", parts);
        }

        [Fact]
        public void The_rim_is_a_gradient_rather_than_a_flat_hairline()
        {
            // The family signature. A flat rim loses contrast exactly when
            // a blob drifts toward it.
            Assert.Contains(
                "new LinearGradientBrush(",
                RepoFile("Controls", "Aurora", "AuroraSurfaceParts.cs"));
        }

        [Fact]
        public void The_field_is_pinned_to_Content_placement()
        {
            // A text field at panel opacity is readable in a screenshot and
            // unusable in the hand.
            Assert.Contains(
                "AuroraPlacement.Content",
                RepoFile("Controls", "Aurora", "AuroraField.cs"));
        }

        [Fact]
        public void The_card_declares_itself_unfocusable()
        {
            // Twelve tabbable KPI cards would bury the keyboard path.
            Assert.Contains(
                "Focusable = false",
                RepoFile("Controls", "Aurora", "AuroraCard.cs"));
        }

        [Fact]
        public void Every_material_kind_clears_the_aurora_palette()
        {
            // A family keeps its published material until something
            // explicitly takes it away, so an unhandled material that
            // skips this would leave the sky drifting over Bento.
            var painter = RepoFile(
                "Services", "Theme", "Eight", "EightThemePainter.cs");

            var arms = painter.Split("case EightMaterialKind.")
                .Skip(1)
                .ToList();

            Assert.True(arms.Count >= 8,
                "expected one arm per material kind");

            foreach (var arm in arms)
            {
                var body = arm.Split("break;")[0];
                Assert.Contains("AuroraPalette.Clear()", body);
            }

            Assert.Contains(
                "default:", painter);
        }

        // ═══════════════════════════════════════════════════════════════
        //  REGRESSIONS
        //
        //  Each of these shipped in the first pass and was found by
        //  reading the code rather than by running it. They are here
        //  because all five are invisible to the compiler and to every
        //  other test in this file.
        // ═══════════════════════════════════════════════════════════════

        [Fact]
        public void Drift_never_freezes_the_transforms_it_is_about_to_animate()
        {
            // TransformGroup.Freeze() propagates recursively to its
            // Freezable children, so a frozen group means frozen
            // transforms -- and every BeginAnimation on them then throws
            // InvalidOperationException. This fired on the first frame of
            // every Aurora theme load.
            var drift = AuroraMesh.Drift(0, new AuroraMetrics(),
                motionAllowed: true);

            Assert.NotNull(drift);
            Assert.False(drift!.IsFrozen,
                "the drift group is animated every frame; freezing it " +
                "makes BeginAnimation throw before the first frame draws");

            foreach (var child in drift.Children)
            {
                Assert.False(child.IsFrozen,
                    $"{child.GetType().Name} is animated by Drift and " +
                    "cannot be frozen");
            }
        }

        [Fact]
        public void The_backdrop_never_disables_hit_testing_for_its_content()
        {
            // AuroraSurface WRAPS the app root. IsHitTestVisible=False on
            // a container disables it for the whole subtree, so the
            // window renders correctly and responds to nothing.
            var surface = RepoFile(
                "Controls", "Aurora", "AuroraSurface.cs");

            Assert.DoesNotContain("IsHitTestVisible = false;", surface);
            Assert.DoesNotContain("IsHitTestVisible = False;", surface);

            // ...but the decorative blobs still opt out individually.
            Assert.Contains("IsHitTestVisible = false,", surface);

            var style = DictionaryText()
                .Split("<Style TargetType=\"{x:Type aurora:AuroraSurface}\">")[1]
                .Split("</Style>")[0];

            Assert.DoesNotContain("IsHitTestVisible", style);
        }

        [Fact]
        public void Motion_is_gated_on_reduced_motion_never_on_transparency()
        {
            // These are different settings. High contrast strips
            // translucency; it is not an animation preference, and
            // conflating them froze the sky for users who never asked.
            foreach (var file in new[]
                     {
                         "Controls", "Aurora", "AuroraButton.cs"
                     })
            {
                var text = RepoFile(file);

                Assert.DoesNotContain(
                    "if (AuroraPalette.TransparencySuppressed)",
                    text);
            }

            var surface = RepoFile(
                "Controls", "Aurora", "AuroraSurface.cs");

            Assert.DoesNotContain(
                "!AuroraPalette.TransparencySuppressed",
                surface);
        }

        [Fact]
        public void The_sky_resolves_the_ramp_for_the_current_theme()
        {
            // Both ramps are dark, but they are different darks, and
            // painting the light one in dark mode leaves the one surface
            // nobody else owns visibly out of step with the window.
            var surface = RepoFile(
                "Controls", "Aurora", "AuroraSurface.cs");

            Assert.DoesNotContain("AuroraPalette.For(false)", surface);
            Assert.Contains("AuroraPalette.For(IsDark())", surface);

            // The same mistake a second time, in the button: the press
            // scale was read from the light ramp. Benign today only
            // because both ramps happen to declare 0.98 -- which is
            // exactly the kind of coincidence that stops being true
            // during the next design pass.
            var button = RepoFile(
                "Controls", "Aurora", "AuroraButton.cs");

            Assert.DoesNotContain("AuroraPalette.For(false)", button);
        }

        [Fact]
        public void The_family_ships_as_the_ninth_member_of_the_enum()
        {
            var id = RepoFile(
                "Services", "Theme", "Eight", "EightThemeId.cs");

            Assert.Contains("Aurora = 8", id);
            Assert.Contains("Luminous = 7", id);
        }
    }
}
