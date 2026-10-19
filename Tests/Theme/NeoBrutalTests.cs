// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Theme/NeoBrutalTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// The Neo-Brutalism family, pinned.
//
// ── WHY THE ASSERTIONS ARE ABOUT RULES, NOT NUMBERS ──────────────────
//
// "The radius is 0" is a test that only protects a number. "There is no
// BlurEffect anywhere in the family" is a test that protects the one
// property the style is defined by, and it fails loudly if someone
// eventually adds one because it looked nice -- which is the single most
// likely way this family dies.
//
// So the centre of this file is the three rules, the collapsing press,
// the gutter constraint and the ink contract. The last block is
// source-level, because a template that drops PART_NeoShadow compiles,
// passes everything above, and renders a block with no depth at all.
using System;
using System.IO;
using System.Linq;
using System.Windows.Media;
using ProGlassAutomation.Services.Theme.NeoBrutal;
using ProGlassAutomation.Services.Theme.SoftUi;
using Xunit;

namespace ProGlassAutomation.Tests.Theme
{
    public class NeoBrutalTests : IDisposable
    {
        private static readonly Color Paper = Color.FromRgb(0xFF, 0xFD, 0xF5);
        private static readonly Color Surface = Colors.White;
        private static readonly Color Ink = Color.FromRgb(0x0A, 0x0A, 0x0A);
        private static readonly Color Muted = Color.FromRgb(0x4A, 0x4A, 0x4A);
        private static readonly Color Border = Color.FromRgb(0x0A, 0x0A, 0x0A);
        private static readonly Color Shadow = Color.FromRgb(0x0A, 0x0A, 0x0A);
        private static readonly Color Acid = Color.FromRgb(0xFF, 0xDE, 0x00);

        public NeoBrutalTests() => PublishAll();

        public void Dispose() => NeoBrutalPalette.Clear();

        private static void PublishAll()
        {
            NeoBrutalPalette.Publish(
                lightPage: Paper, lightSurface: Surface, lightInk: Ink,
                lightMuted: Muted, lightBorder: Border, lightShadow: Shadow,
                lightAccent: Acid, lightAccentForeground: Ink,
                lightDanger: Acid, lightInfo: Acid, lightSuccess: Acid,
                darkPage: Color.FromRgb(0x16, 0x13, 0x0F),
                darkSurface: Color.FromRgb(0x2A, 0x24, 0x1C),
                darkInk: Color.FromRgb(0xFF, 0xFD, 0xF5),
                darkMuted: Color.FromRgb(0xB0, 0xA8, 0x96),
                darkBorder: Colors.White, darkShadow: Acid,
                darkAccent: Acid, darkAccentForeground: Ink,
                darkDanger: Acid, darkInfo: Acid, darkSuccess: Acid);
        }

        private static NeoBrutalProfile Profile(bool dark = false) =>
            NeoBrutalPalette.For(dark)!;

        private static NeoBrutalStack Resolve(
            NeoBrutalPlacement placement =
                NeoBrutalPlacement.Control,
            SoftInteractionState state = SoftInteractionState.Rest,
            bool interactive = false,
            bool selected = false,
            bool dark = false)
        {
            var recipe = new NeoBrutalRecipe(Profile(dark), dark);
            return recipe.Resolve(
                placement, SoftDepthTier.Resting, state,
                interactive, selected);
        }

        // ═══════════════════════════════════════════════════════════════
        //  RULE 3 · RADIUS ZERO
        // ═══════════════════════════════════════════════════════════════

        [Fact]
        public void The_radius_is_zero_and_every_stack_reports_it()
        {
            Assert.Equal(0.0, new NeoBrutalMetrics().Radius);

            foreach (NeoBrutalPlacement p
                     in Enum.GetValues<NeoBrutalPlacement>())
            {
                Assert.Equal(0.0, Resolve(p).Radius);
                Assert.True(Resolve(p).HasRadius);
            }
        }

        // ═══════════════════════════════════════════════════════════════
        //  RULE 2 · A HARD SHADOW, WHICH IS THE OPPOSITE OF SWISS
        // ═══════════════════════════════════════════════════════════════

        [Fact]
        public void Every_control_stack_carries_exactly_one_shadow()
        {
            foreach (NeoBrutalPlacement p
                     in Enum.GetValues<NeoBrutalPlacement>())
            {
                var stack = Resolve(p);

                if (p == NeoBrutalPlacement.Page) continue;

                Assert.True(stack.HasShadow);
                Assert.Single(stack.Shadows);
            }
        }

        [Fact]
        public void The_shadow_is_the_inverse_of_swiss_on_both_axes()
        {
            // Swiss: radius 0 AND no shadow. This family: radius 0 AND a
            // shadow. Between them they are why radius reads as a dial in
            // this engine rather than as an identity.
            var stack = Resolve(NeoBrutalPlacement.Control);

            Assert.Equal(0.0, stack.Radius);
            Assert.True(stack.HasShadow);
            Assert.Single(stack.Shadows);
        }

        [Fact]
        public void The_shadow_offset_is_always_a_whole_stamp_not_a_soft_falloff()
        {
            // The offsets are integers on purpose. A fractional offset on
            // a hard shadow reads as a misalignment, not as refinement.
            var m = new NeoBrutalMetrics();

            foreach (var offset in new[]
                     { m.ShadowSmall, m.ShadowMedium, m.ShadowLarge })
            {
                Assert.Equal(
                    Math.Round(offset),
                    offset,
                    6);
            }
        }

        [Fact]
        public void The_shadow_is_opaque_and_never_translucent()
        {
            // A translucent offset plate is a blurred shadow wearing a
            // costume.
            var cell = Resolve().Cell;

            Assert.Equal(255, (int)cell.ShadowColour.A);
        }

        // ═══════════════════════════════════════════════════════════════
        //  RULE 1 · A BORDER THICK ENOUGH TO READ AS ORNAMENT
        // ═══════════════════════════════════════════════════════════════

        [Fact]
        public void The_border_is_never_a_hairline()
        {
            // "At 1px a border is a hairline that separates two regions.
            // At 4px it becomes a graphic element in its own right."
            var m = new NeoBrutalMetrics();

            Assert.True(m.BorderPrimary >= 2);
            Assert.True(m.BorderThin >= 2);
            Assert.True(m.BorderThick > m.BorderPrimary);

            var cell = Resolve().Cell;
            Assert.True(cell.BorderWidth >= m.BorderPrimary);
        }

        // ═══════════════════════════════════════════════════════════════
        //  THE PRESS · A STAMP, AND IMPACT HAS NO CURVE
        // ═══════════════════════════════════════════════════════════════

        [Fact]
        public void Press_collapses_the_shadow_to_zero_and_moves_the_block_into_it()
        {
            var m = new NeoBrutalMetrics();

            var rest = Resolve(NeoBrutalPlacement.Control);
            var press = Resolve(NeoBrutalPlacement.Control,
                SoftInteractionState.Pressed, interactive: true);

            Assert.Equal(0.0, press.Cell.ShadowOffset);

            // The block travels by EXACTLY the offset it had. That
            // symmetry is the gesture; anything less looks like a
            // separate animation rather than a press.
            Assert.Equal(rest.Cell.ShadowOffset, press.Cell.Lift, 3);
            Assert.Equal(m.ShadowMedium, press.Cell.Lift, 3);
        }

        [Fact]
        public void Press_also_thickens_the_border_so_state_is_never_shadow_only()
        {
            // "Offset shadows must not be the only difference between
            // resting and pressed; move the block AND change the border
            // weight together."
            var rest = Resolve(NeoBrutalPlacement.Control);
            var press = Resolve(NeoBrutalPlacement.Control,
                SoftInteractionState.Pressed, interactive: true);

            Assert.True(
                press.Cell.BorderWidth > rest.Cell.BorderWidth,
                "movement alone is not enough; the border weight is the " +
                "signal that survives reduced motion");
        }

        [Fact]
        public void Press_translates_and_never_adjusts_margin_or_padding()
        {
            // "The press must translate, not adjust margin or padding, or
            // the whole board jumps on every click."
            var parts = RepoFile(
                "Controls", "NeoBrutal", "NeoBrutalSurfaceParts.cs");

            Assert.Contains(
                "new TranslateTransform(c.Lift, c.Lift)", parts);
            Assert.DoesNotContain("Fill.Margin", parts);
            Assert.DoesNotContain("Fill.Padding", parts);
        }

        [Fact]
        public void The_press_duration_is_zero_because_impact_has_no_curve()
        {
            Assert.Equal(0.0, new NeoBrutalMetrics().PressDuration);
        }

        [Fact]
        public void Hover_lifts_the_block_and_grows_the_shadow()
        {
            var m = new NeoBrutalMetrics();

            var rest = Resolve(NeoBrutalPlacement.Control);
            var hover = Resolve(NeoBrutalPlacement.Control,
                SoftInteractionState.Hover, interactive: true);

            Assert.Equal(m.HoverLift, hover.Cell.Lift, 3);
            Assert.True(
                hover.Cell.ShadowOffset > rest.Cell.ShadowOffset,
                "a shadow that SHRINKS on hover would read as the block " +
                "coming closer, which is the wrong direction");
        }

        [Fact]
        public void Disabled_drops_the_shadow_entirely_rather_than_fading_it()
        {
            var off = Resolve(NeoBrutalPlacement.Control,
                SoftInteractionState.Disabled);

            Assert.False(off.Cell.ShadowVisible);
            Assert.Equal(0.0, off.Cell.ShadowOffset);
            Assert.Empty(off.Shadows);
        }

        // ═══════════════════════════════════════════════════════════════
        //  THE GUTTER IS A CORRECTNESS CONSTRAINT
        // ═══════════════════════════════════════════════════════════════

        [Fact]
        public void The_minimum_gutter_exceeds_the_largest_shadow_offset()
        {
            // "Equal-offset hard shadows on a dense grid collide where two
            // shadows meet a gap. Keep the grid gap larger than the
            // offset." A soft shadow fades; a hard one does not, so
            // touching plates merge into a black wedge.
            var m = new NeoBrutalMetrics();

            Assert.True(
                m.MinimumGutter > m.ShadowLarge,
                $"gutter {m.MinimumGutter} must exceed the largest offset " +
                $"{m.ShadowLarge} or dense layouts wedge");
        }

        // ═══════════════════════════════════════════════════════════════
        //  THE INK CONTRACT · BLACK ON EVERY ACCENT, BOTH RAMPS
        // ═══════════════════════════════════════════════════════════════

        [Fact]
        public void The_accent_label_is_ink_in_the_LIGHT_ramp()
        {
            Assert.True(
                NeoBrutalPalette.Contrast(
                    Profile(false).AccentForeground,
                    Profile(false).Accent)
                >= NeoBrutalPalette.MinimumBodyContrast);
        }

        [Fact]
        public void The_accent_label_is_still_ink_in_the_DARK_ramp()
        {
            // REGRESSION. Build() was handed the CURRENT MODE's body ink
            // as its "prefer black" target. In the dark ramp that ink is
            // near-white, so the preference resolved to white -- and
            // white on acid yellow measures 1.34:1. Every dark-mode
            // primary button would have been unreadable.
            var profile = Profile(true);

            Assert.True(
                NeoBrutalPalette.Contrast(
                    profile.AccentForeground, profile.Accent)
                >= NeoBrutalPalette.MinimumBodyContrast,
                "the accent label must resolve to the family's true ink " +
                "in BOTH ramps, not to whatever the mode's body ink is");
        }

        [Fact]
        public void White_on_acid_is_the_failure_this_family_exists_to_avoid()
        {
            // Pinned so nobody "modernises" the accent ink to white.
            var acid = Profile(false).Accent;

            Assert.True(
                NeoBrutalPalette.Contrast(Colors.White, acid)
                    < NeoBrutalPalette.MinimumBodyContrast,
                "white on the accent is expected to FAIL; if this ever " +
                "passes the accent changed and this guard is stale");
        }

        [Fact]
        public void The_border_clears_3_to_1_against_BOTH_the_fill_and_the_page()
        {
            // A border that clears the fill can still vanish into the page
            // behind it. The documented failure is a black border on a
            // dark section, so both are measured independently.
            foreach (var dark in new[] { false, true })
            {
                var p = Profile(dark);
                var cell = Resolve(
                    NeoBrutalPlacement.Content, dark: dark).Cell;

                Assert.True(
                    NeoBrutalPalette.Contrast(cell.Border, p.Surface)
                        >= NeoBrutalPalette.MinimumRuleContrast);
                Assert.True(
                    NeoBrutalPalette.Contrast(cell.Border, p.Page)
                        >= NeoBrutalPalette.MinimumRuleContrast);
            }
        }

        [Fact]
        public void Muted_ink_is_body_text_not_decorative_grey()
        {
            foreach (var dark in new[] { false, true })
            {
                Assert.True(
                    NeoBrutalPalette.Contrast(
                        Profile(dark).MutedInk, Profile(dark).Page)
                    >= NeoBrutalPalette.MinimumBodyContrast);
            }
        }

        [Fact]
        public void A_selected_block_is_the_accent_fill_with_accent_ink()
        {
            var profile = Profile(false);

            var stack = Resolve(NeoBrutalPlacement.Control,
                selected: true);

            Assert.Equal(profile.Accent, stack.Cell.Fill);
            Assert.Equal(profile.AccentForeground, stack.Cell.Ink);
        }

        [Fact]
        public void The_page_placement_is_the_paper_and_carries_no_depth()
        {
            var stack = Resolve(NeoBrutalPlacement.Page);

            Assert.Equal(0.0, stack.Cell.BorderWidth);
            Assert.False(stack.Cell.ShadowVisible);
        }

        // ═══════════════════════════════════════════════════════════════
        //  SOURCE INVARIANTS
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
            RepoFile("Themes", "NeoBrutal", "NeoBrutalControls.xaml");

        private static string FamilySource(params string[] parts) =>
            RepoFile(new[] { "Controls", "NeoBrutal" }
                .Concat(parts).ToArray());

        /// <summary>
        /// Strips // comments before an ABSENCE assertion.
        /// <para>
        /// NeoBrutalSurfaceParts.cs contains the literal words
        /// "BlurEffect" and "DropShadowEffect" in the comment that
        /// explains why it uses neither. Asserting DoesNotContain on raw
        /// source therefore FAILED on the file it was written to guard --
        /// which is the worst possible failure mode for a test that is
        /// supposed to be load-bearing. Comment text is prose; only code
        /// counts.
        /// </para>
        /// </summary>
        private static string CodeOnly(string source) =>
            string.Join("\n", source
                .Split('\n')
                .Select(line =>
                {
                    int i = line.IndexOf("//", StringComparison.Ordinal);
                    return i >= 0 ? line.Substring(0, i) : line;
                }));

        [Fact]
        public void The_family_instantiates_no_blur_effect_anywhere()
        {
            // The zero blur is the load-bearing property of the style.
            // One BlurEffect and it reads as a mistake.
            var parts = CodeOnly(FamilySource("NeoBrutalSurfaceParts.cs"));
            var button = CodeOnly(FamilySource("NeoBrutalButton.cs"));

            Assert.DoesNotContain("BlurEffect", parts);
            Assert.DoesNotContain("BlurEffect", button);
        }

        [Fact]
        public void The_family_uses_no_drop_shadow_effect()
        {
            // The hard offset is an OFFSET PLATE, not an effect pass.
            var parts = CodeOnly(FamilySource("NeoBrutalSurfaceParts.cs"));

            Assert.DoesNotContain("DropShadowEffect", parts);
        }

        [Fact]
        public void The_family_starts_no_animation_at_all()
        {
            // "Impact has no curve." Any Storyboard here is a curve.
            var parts = CodeOnly(FamilySource("NeoBrutalSurfaceParts.cs"));
            var button = CodeOnly(FamilySource("NeoBrutalButton.cs"));

            Assert.DoesNotContain("BeginAnimation", parts);
            Assert.DoesNotContain("DoubleAnimation", parts);
            Assert.DoesNotContain("BeginAnimation", button);
            Assert.DoesNotContain("DoubleAnimation", button);
        }

        [Fact]
        public void Every_corner_radius_in_the_dictionary_is_an_explicit_zero()
        {
            var text = DictionaryText();

            var radii = System.Text.RegularExpressions.Regex
                .Matches(text, @"CornerRadius=""([^""]*)""")
                .Select(m => m.Groups[1].Value)
                .ToList();

            Assert.True(radii.Count > 0, "expected explicit corner radii");
            Assert.All(radii, r => Assert.Equal("0", r));
        }

        [Fact]
        public void Every_template_declares_all_three_parts()
        {
            var text = DictionaryText();

            var templates = text.Split("<ControlTemplate x:Key=\"")
                .Skip(1)
                .Select(chunk => chunk.Split('>')[0])
                .Select(key => key.Split('"')[0])
                .Where(key => key.StartsWith("Neo"))
                .ToList();

            Assert.Equal(3, templates.Count);

            foreach (var name in templates)
            {
                var body = text.Split(
                    $"<ControlTemplate x:Key=\"{name}\"")[1]
                    .Split("</ControlTemplate>")[0];

                Assert.Contains("PART_NeoFill", body);
                Assert.Contains("PART_NeoShadow", body);
                Assert.Contains("PART_NeoFocus", body);
            }
        }

        [Fact]
        public void Every_template_reserves_room_for_its_own_shadow()
        {
            // A hard offset extends past the control's bounds and does
            // not fade, so a parent clips it without this margin. A
            // blurred shadow forgives the same omission.
            var text = DictionaryText();

            var templates = text.Split("<ControlTemplate x:Key=\"")
                .Skip(1)
                .Select(chunk => chunk.Split('>')[0])
                .Select(key => key.Split('"')[0])
                .Where(key => key.StartsWith("Neo"))
                .ToList();

            foreach (var name in templates)
            {
                var body = text.Split(
                    $"<ControlTemplate x:Key=\"{name}\"")[1]
                    .Split("</ControlTemplate>")[0];

                Assert.Contains("NeoShadowRoom", body);
            }
        }

        [Fact]
        public void The_focus_outline_is_declared_outside_the_border()
        {
            // Focus states get eaten: a 4px black border swallows a focus
            // ring drawn inside the control.
            var parts = FamilySource("NeoBrutalSurfaceParts.cs");

            Assert.Contains(
                "-(c.BorderWidth + m.FocusOffset)", CodeOnly(parts));
        }

        [Fact]
        public void The_field_template_names_PART_ContentHost()
        {
            // Without that exact name a TextBox renders, measures, and
            // accepts no keystrokes at all.
            Assert.Contains("PART_ContentHost", DictionaryText());
        }

        [Fact]
        public void The_card_declares_itself_unfocusable()
        {
            Assert.Contains(
                "Focusable = false",
                CodeOnly(FamilySource("NeoBrutalCard.cs")));
        }

        [Fact]
        public void Every_material_kind_clears_the_neo_brutal_palette()
        {
            var painter = RepoFile(
                "Services", "Theme", "Eight", "EightThemePainter.cs");

            var arms = painter.Split("case EightMaterialKind.")
                .Skip(1).ToList();

            Assert.True(arms.Count >= 9,
                "expected one arm per material kind");

            foreach (var arm in arms)
            {
                Assert.Contains(
                    "NeoBrutalPalette.Clear()", arm.Split("break;")[0]);
            }
        }

        [Fact]
        public void The_engine_declares_a_hard_edge_elevation_kind()
        {
            var file = RepoFile(
                "Services", "Theme", "Eight", "EightThemeProfile.cs");

            Assert.Contains("Stamp = 5", file);
        }

        [Fact]
        public void The_shipped_profile_publishes_zero_blur_for_this_family()
        {
            var text = RepoFile(
                "Services", "Theme", "Eight", "EightThemeProfiles.cs");

            // Anchored on the method signature rather than the comment
            // banner: the banner text drifted between drafts, and a split
            // anchor that no longer exists silently yields the WHOLE
            // file, which then trivially "passes" any Contains below.
            Assert.Contains(
                "private static EightThemeProfile NeoBrutalism()", text);

            var neo = text.Split(
                "private static EightThemeProfile NeoBrutalism()")[1];
            neo = neo.Split("private static")[0];

            Assert.Contains("EightElevationKind.Stamp", neo);
            Assert.Contains("lowBlur: 0", neo);
            Assert.Contains("highBlur: 0", neo);
        }
    }
}
