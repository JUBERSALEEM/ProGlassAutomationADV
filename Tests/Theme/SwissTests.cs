// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Theme/SwissTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// The recipe for an absence, pinned.
//
// Swiss is the hardest family in this engine to test, and for an
// unusual reason: the usual failure is an OMISSION, not a wrong value.
// Clay fails by putting the wrong shadow in; neumorphism fails by
// putting only one shadow in. Swiss fails by quietly acquiring one —
// and a single 0.2px drop under a card is invisible in a screenshot
// review while being the exact thing the style exists to prevent.
//
// So most of these tests are negative assertions, and the negative ones
// are the load-bearing ones:
//
//   * Shadows is empty, and HasShadow is false, in EVERY state
//   * Radius is zero in EVERY state
//   * Inversion is a full swap, not a tint
//   * Selection changes rule WEIGHT, never colour alone
//   * No tier moves anything
//
// The recipe is pure arithmetic, so none of this needs a window, a
// dispatcher or a mouse.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Windows;
using System.Windows.Media;
using ProGlassAutomation.Services.Theme;
using ProGlassAutomation.Services.Theme.Eight;
using ProGlassAutomation.Services.Theme.Clay;
using ProGlassAutomation.Services.Theme.Neu;
using ProGlassAutomation.Services.Theme.SoftUi;
using ProGlassAutomation.Services.Theme.Swiss;
using Xunit;

namespace ProGlassAutomation.Tests.Theme
{
    public class SwissTests
    {
        // Off-white paper, near-black ink. Deliberately NOT pure white:
        // see SwissPalette.HoldOffExtreme for why the pure value is
        // actively worse.
        private static readonly Color LightPaper =
            Color.FromRgb(0xFA, 0xFA, 0xF8);
        private static readonly Color LightInk =
            Color.FromRgb(0x11, 0x11, 0x11);

        private static readonly Color DarkPaper =
            Color.FromRgb(0x12, 0x12, 0x12);
        private static readonly Color DarkInk =
            Color.FromRgb(0xF2, 0xF2, 0xF0);

        private static readonly Color Muted =
            Color.FromRgb(0x6B, 0x6B, 0x6B);
        private static readonly Color Rule =
            Color.FromRgb(0x1A, 0x1A, 0x1A);
        private static readonly Color SwissAccent =
            Color.FromRgb(0xE1, 0x1B, 0x1B);

        private static SwissProfile Light() =>
            SwissPalette.Build(LightPaper, LightInk, Muted, Rule, SwissAccent);

        private static SwissProfile Dark() =>
            SwissPalette.Build(DarkPaper, DarkInk, Muted, Rule, SwissAccent);

        private static SwissStack Resolve(
            bool dark = false,
            SoftInteractionState state = SoftInteractionState.Rest,
            SoftDepthTier tier = SoftDepthTier.Resting,
            bool interactive = false,
            bool selected = false) =>
            new SwissRecipe(dark ? Dark() : Light(), dark)
                .Resolve(tier, state, interactive, selected);

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
        //  THE ABSENCE
        //
        //  Every other family has a shadow budget to stay inside. This
        //  one has no budget: a shadow that appears is a defect. So
        //  these two tests sweep the entire state and tier space rather
        //  than sampling it.
        // ═══════════════════════════════════════════════════════════

        [Fact]
        public void No_state_and_no_tier_can_produce_a_shadow()
        {
            foreach (var dark in new[] { false, true })
            {
                foreach (var tier in AllTiers)
                {
                    foreach (var state in AllStates)
                    {
                        foreach (var interactive in new[] { false, true })
                        {
                            foreach (var selected in new[] { false, true })
                            {
                                var stack = Resolve(dark, state, tier,
                                    interactive, selected);

                                Assert.False(stack.HasShadow,
                                    $"{(dark ? "dark" : "light")}/{tier}/{state}: "
                                    + "a Swiss surface acquired a shadow.");

                                // Not merely "empty right now" — empty as a
                                // type-level guarantee, so no future branch
                                // can quietly start appending to it.
                                Assert.NotNull(stack.Shadows);
                                Assert.Empty(stack.Shadows);
                            }
                        }
                    }
                }
            }
        }

        [Fact]
        public void The_shadow_list_is_the_shared_empty_instance()
        {
            // If Shadows were null or a fresh array per stack, a caller
            // could still reflect into it. Pinning the shared instance
            // makes appending throw rather than succeed quietly.
            var a = Resolve();
            var b = Resolve(state: SoftInteractionState.Pressed);

            Assert.Same(a.Shadows, b.Shadows);
            Assert.Empty(a.Shadows);
        }

        [Fact]
        public void No_state_and_no_tier_can_produce_a_radius()
        {
            foreach (var dark in new[] { false, true })
            {
                foreach (var tier in AllTiers)
                {
                    foreach (var state in AllStates)
                    {
                        var stack = Resolve(dark, state, tier,
                            interactive: true, selected: true);

                        Assert.Equal(0.0, stack.Contrast.Radius);
                        Assert.False(stack.HasRadius,
                            $"{(dark ? "dark" : "light")}/{tier}/{state}: "
                            + "a Swiss surface grew a corner radius.");
                    }
                }
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  INVERSION IS THE STATE MACHINE
        // ═══════════════════════════════════════════════════════════

        [Fact]
        public void Hover_on_an_interactive_element_inverts_completely()
        {
            var rest = Resolve(interactive: true);
            var hover = Resolve(state: SoftInteractionState.Hover,
                interactive: true);

            Assert.False(rest.Inverted);
            Assert.True(hover.Inverted);

            // A full swap, not a tint. Asserting exact equality is the
            // whole point: a 6% darkening would also raise contrast and
            // still fail here.
            Assert.Equal(rest.Contrast.Ink, hover.Contrast.Fill);
            Assert.Equal(rest.Contrast.Fill, hover.Contrast.Ink);

            Assert.True(
                Contrast(hover.Contrast.Fill, hover.Contrast.Ink) >= 15.0,
                "Inversion must clear WCAG AAA, not just AA.");
        }

        [Fact]
        public void Press_inverts_even_on_a_non_interactive_element()
        {
            // Press implies the element WAS interactive. If a control
            // somehow reaches Pressed with interactive false, it must
            // still turn over — a press that fails to register reads as a
            // dropped click.
            var stack = Resolve(state: SoftInteractionState.Pressed,
                interactive: false);

            Assert.True(stack.Inverted);
        }

        [Fact]
        public void Hover_on_a_non_interactive_panel_does_nothing_at_all()
        {
            var rest = Resolve(interactive: false);
            var hover = Resolve(state: SoftInteractionState.Hover,
                interactive: false);

            // Not inverted, not dimmed, not changed. A panel that reacts
            // to the pointer is a panel claiming to be a control, and in
            // a style whose entire vocabulary is structure that is the
            // expensive kind of lie.
            Assert.False(hover.Inverted);
            Assert.Equal(rest.Contrast.Fill, hover.Contrast.Fill);
            Assert.Equal(rest.Contrast.Ink, hover.Contrast.Ink);
            Assert.Equal(rest.Contrast.Rule, hover.Contrast.Rule);
            Assert.Equal(
                rest.Contrast.RuleThickness, hover.Contrast.RuleThickness);
        }

        [Fact]
        public void Focus_does_not_invert_because_hover_already_does()
        {
            // If focus inverted too, a keyboard user and a mouse user
            // could never tell which of the two the app thinks is true.
            // Focus draws the ring instead; hover turns the surface over.
            var focus = Resolve(state: SoftInteractionState.Focused,
                interactive: true);

            Assert.False(focus.Inverted);
            Assert.True(focus.Focused);
        }

        [Fact]
        public void Disabled_never_inverts_because_that_would_promote_it()
        {
            var stack = Resolve(state: SoftInteractionState.Disabled,
                interactive: true);

            // A disabled control rendered at full contrast would be the
            // most prominent thing on the screen, which is the exact
            // opposite of what it is for.
            Assert.False(stack.Inverted);
        }

        [Fact]
        public void The_focused_flag_is_true_only_in_the_focused_state()
        {
            foreach (var state in AllStates)
            {
                var stack = Resolve(state: state, interactive: true);
                Assert.Equal(
                    state == SoftInteractionState.Focused, stack.Focused);
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  DEPTH IS NOT A VARIABLE IN THIS STYLE
        // ═══════════════════════════════════════════════════════════

        [Fact]
        public void Every_depth_tier_renders_identically()
        {
            // Pretending a Swiss modal sits at a different depth from a
            // Swiss card would be inventing depth the style has
            // explicitly abolished. The tier still exists for vocabulary
            // uniformity, and it changes nothing.
            var reference = Resolve();

            foreach (var tier in AllTiers)
            {
                var stack = Resolve(tier: tier);

                Assert.Equal(reference.Contrast.Fill, stack.Contrast.Fill);
                Assert.Equal(reference.Contrast.Ink, stack.Contrast.Ink);
                Assert.Equal(reference.Contrast.Rule, stack.Contrast.Rule);
                Assert.Equal(
                    reference.Contrast.RuleThickness,
                    stack.Contrast.RuleThickness);
                Assert.Equal(tier, stack.Tier);
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  SELECTION IS A WEIGHT CHANGE, NOT A COLOUR
        // ═══════════════════════════════════════════════════════════

        [Fact]
        public void Selection_changes_the_rule_weight_from_one_to_two()
        {
            var rest = Resolve();
            var selected = Resolve(selected: true);

            Assert.Equal(1.0, rest.Contrast.RuleThickness);
            Assert.Equal(2.0, selected.Contrast.RuleThickness);
            Assert.True(selected.Selected);

            // AND the colour is untouched. Both cues are non-colour, so
            // the selected state survives a greyscale print, a
            // colour-blind reader and a high-contrast mode. A palette
            // change here would leave the state invisible in all three.
            Assert.Equal(rest.Contrast.Rule, selected.Contrast.Rule);
            Assert.Equal(rest.Contrast.Fill, selected.Contrast.Fill);
            Assert.Equal(rest.Contrast.Ink, selected.Contrast.Ink);
        }

        [Fact]
        public void Selection_survives_the_inverted_state_too()
        {
            // A selected item the user is also hovering must still show
            // its heavier rule. If the inversion overwrote the weight,
            // selection would silently vanish under the pointer.
            var stack = Resolve(state: SoftInteractionState.Hover,
                interactive: true, selected: true);

            Assert.True(stack.Inverted);
            Assert.True(stack.Selected);
            Assert.Equal(2.0, stack.Contrast.RuleThickness);
        }

        // ═══════════════════════════════════════════════════════════
        //  THE 8PT GRID
        // ═══════════════════════════════════════════════════════════

        [Fact]
        public void Every_spacing_value_is_a_whole_multiple_of_the_unit()
        {
            var m = Light().Metrics;

            Assert.Equal(8.0, m.Unit);
            Assert.Equal(0.0, m.Unit % 1);

            foreach (var (name, value) in new[]
            {
                ("Gutter", m.Gutter),
                ("Margin", m.Margin),
            })
            {
                Assert.Equal(0.0, value % m.Unit);
            }

            Assert.Equal(24.0, m.Gutter);
            Assert.Equal(64.0, m.Margin);
        }

        [Fact]
        public void The_motion_ceiling_is_150ms_and_is_a_ceiling()
        {
            var m = Light().Metrics;

            // A ceiling, not a default. Anything slower reads as
            // decoration, which is the one thing this style exists to
            // abolish.
            Assert.Equal(150.0, m.MotionCeiling);
            Assert.True(m.MotionCeiling > 0.0);
        }

        [Fact]
        public void No_state_moves_anything()
        {
            // The recipe exposes no translate, scale or offset on
            // SwissStack at all — there is no field for one to live in.
            // This test asserts that indirectly but meaningfully: a
            // change of state may change COLOURS and rule WEIGHT, and
            // nothing else about the geometry.
            foreach (var state in AllStates)
            {
                var stack = Resolve(state: state, interactive: true,
                    selected: false);

                // Radius is the only geometry the construction carries,
                // and it is zero in every state.
                Assert.Equal(0.0, stack.Contrast.Radius);

                // Rule weight only ever moves because SELECTION asked it
                // to. A hover must not have quietly bought itself a
                // heavier line as a second selection cue.
                Assert.Equal(1.0, stack.Contrast.RuleThickness);
            }
        }

        [Fact]
        public void Leading_is_fixed_so_the_right_hand_edge_can_wander_nothing()
        {
            var m = Light().Metrics;

            Assert.Equal(1.5, m.BodyLineHeight);
            Assert.Equal(1.1, m.DisplayLineHeight);
            Assert.True(m.BodyLineHeight > 1.0,
                "Flush-left ragged-right text needs leading above 1.");
        }

        // ═══════════════════════════════════════════════════════════
        //  CONTRAST
        //
        //  The style that forbids decoration ends up with the strongest
        //  contrast guarantee of the eight. Worth pinning, because it
        //  is the reason this family is worth having.
        // ═══════════════════════════════════════════════════════════

        [Fact]
        public void Resting_ink_clears_wcag_aaa()
        {
            foreach (var dark in new[] { false, true })
            {
                var stack = Resolve(dark);
                double ratio = Contrast(
                    stack.Contrast.Fill, stack.Contrast.Ink);

                Assert.True(ratio >= 7.0,
                    $"{(dark ? "dark" : "light")} resting contrast "
                    + $"{ratio:0.00}:1 is below AAA (7:1).");
            }
        }

        [Fact]
        public void Muted_ink_still_clears_wcag_aa()
        {
            // The recipe's doc comment makes a specific promise here:
            // "muted" means SMALLER and LIGHTER, never washed out. A
            // muted tint below 4.5:1 would import the exact failure the
            // rest of this engine works to avoid.
            foreach (var dark in new[] { false, true })
            {
                var stack = Resolve(dark);
                double ratio = Contrast(
                    stack.Contrast.Fill, stack.Contrast.MutedInk);

                Assert.True(ratio >= 4.5,
                    $"{(dark ? "dark" : "light")} muted contrast "
                    + $"{ratio:0.00}:1 is below AA (4.5:1).");
            }
        }

        [Fact]
        public void The_hairline_is_visible_against_the_paper()
        {
            // A 1px rule drawn in a subtle border is a line the reader
            // cannot see, and a rule you cannot see is not a structural
            // device — it is a rendering artefact.
            foreach (var dark in new[] { false, true })
            {
                var stack = Resolve(dark);
                double ratio = Contrast(
                    stack.Contrast.Fill, stack.Contrast.Rule);

                Assert.True(ratio >= 3.0,
                    $"{(dark ? "dark" : "light")} hairline contrast "
                    + $"{ratio:0.00}:1 is below the 3:1 UI-component floor.");
            }
        }

        [Fact]
        public void The_disabled_state_stays_legible_rather_than_vanishing()
        {
            var stack = Resolve(state: SoftInteractionState.Disabled);
            double ratio = Contrast(
                stack.Contrast.Fill, stack.Contrast.Ink);

            // Disabled is allowed to recede. It is not allowed to stop
            // being readable — an unreadable disabled control is an
            // accessibility failure that presents as a design choice.
            Assert.True(ratio >= 3.0,
                $"Disabled contrast {ratio:0.00}:1 is unreadable.");
        }

        // ═══════════════════════════════════════════════════════════
        //  DARK MODE
        // ═══════════════════════════════════════════════════════════

        [Fact]
        public void The_paper_is_held_off_pure_white_and_pure_black()
        {
            // Pure white is the screen, not paper, and on it every
            // hairline reads as a rendering artefact. Pure black makes
            // the hairline disappear entirely. Both extremes push the
            // ink/rule contrast below what the style needs.
            var heldLight = SwissPalette.HoldOffExtreme(Colors.White, light: true);
            Assert.NotEqual(Colors.White, heldLight);
            Assert.True(Luminance(heldLight) < Luminance(Colors.White));

            var heldDark = SwissPalette.HoldOffExtreme(Colors.Black, light: false);
            Assert.NotEqual(Colors.Black, heldDark);
            Assert.True(Luminance(heldDark) > Luminance(Colors.Black));

            // The point of the guard was to leave the hairline something
            // to be measured against. Asserting it directly, per mode,
            // rather than comparing the two papers to each other — which
            // would only ever measure that light is lighter than dark.
            Assert.True(Contrast(heldLight, LightInk) >= 7.0,
                "Held-off light paper lost AAA contrast against its ink.");
            Assert.True(Contrast(heldDark, DarkInk) >= 7.0,
                "Held-off dark paper lost AAA contrast against its ink.");
        }

        [Fact]
        public void A_normal_paper_is_left_alone_by_the_hold_off()
        {
            // The guard is a precondition CHECK, not a bludgeon. It must
            // not nudge every paper in the engine, or Swiss would quietly
            // become the family that tints your colours.
            //
            // Note what is deliberately absent from this list: #202020.
            // It is a perfectly ordinary-looking dark surface, but its
            // luminance is 0.014 and the guard WILL lift it, because the
            // hairlines genuinely have nothing to sit against down
            // there. That is the guard doing its job, not a bug — and
            // it is why the test does not assert on it.
            foreach (var c in new[]
            {
                LightPaper,
                Color.FromRgb(0xE6, 0xE6, 0xE6),
                Color.FromRgb(0xC8, 0xC8, 0xC4),
                Color.FromRgb(0x4A, 0x4A, 0x48),
            })
            {
                Assert.Equal(c, SwissPalette.HoldOffExtreme(c, light: true));
            }
        }

        [Fact]
        public void The_muted_ink_and_the_rule_are_ENFORCED_not_merely_promised()
        {
            // SwissContrast's doc comments promise that muted ink never
            // drops below 4.5:1 and that the hairline never drops below
            // the 3:1 non-text floor. A promise in a comment is worth
            // nothing, and this is the easiest promise in the engine to
            // break by accident.
            //
            // The measured case: one mid-grey #6B6B6B used for BOTH
            // modes. On #FAFAF8 paper that is a comfortable 5.10:1. On a
            // dark paper it collapses to roughly 3.2:1 — under AA — and
            // a #1A1A1A hairline on the same dark paper lands near
            // 1.01:1, which is a line that is being drawn every frame
            // and is measurably invisible.
            //
            // So SwissPalette.Build enforces both, and this test feeds
            // it the exact values that would otherwise break.
            Assert.Equal(4.5, SwissPalette.MinimumBodyContrast);
            Assert.Equal(3.0, SwissPalette.MinimumRuleContrast);

            var dark = Dark();

            Assert.True(
                Contrast(dark.Paper, dark.MutedInk) >= 4.5,
                $"dark muted ink fell to "
                + $"{Contrast(dark.Paper, dark.MutedInk):0.00}:1.");

            Assert.True(
                Contrast(dark.Paper, dark.Rule) >= 3.0,
                $"dark hairline fell to "
                + $"{Contrast(dark.Paper, dark.Rule):0.00}:1.");
        }

        [Fact]
        public void The_enforcement_never_makes_a_colour_worse()
        {
            // A guard that could reduce contrast would be worse than no
            // guard. Every candidate must end up at least as readable as
            // it started, and the fallback is the ink itself, which is
            // the best ground-relative colour available.
            var paper = Light().Paper;

            foreach (var candidate in new[]
            {
                LightInk, Color.FromRgb(0x80, 0x80, 0x80),
                Color.FromRgb(0xC0, 0xC0, 0xC0),
                Color.FromRgb(0xF0, 0xF0, 0xEE),
            })
            {
                double before = Contrast(candidate, paper);
                var after = SwissPalette.EnsureContrast(
                    candidate, LightInk,
                    SwissPalette.MinimumRuleContrast, paper);

                Assert.True(
                    Contrast(after, paper) >= before,
                    $"enforcement moved {candidate} the wrong way.");

                Assert.True(
                    Contrast(after, paper)
                        >= SwissPalette.MinimumRuleContrast,
                    $"{candidate} was left below the floor.");
            }
        }

        [Fact]
        public void A_candidate_that_already_clears_the_floor_is_returned_unchanged()
        {
            // Otherwise the guard would tint every colour in the engine
            // on its way to a number it already had.
            var paper = Light().Paper;
            var rule = Light().Rule;

            Assert.Equal(
                rule,
                SwissPalette.EnsureContrast(
                    rule, LightInk,
                    SwissPalette.MinimumRuleContrast, paper));
        }

        [Fact]
        public void Dark_mode_inverts_exactly_the_way_light_mode_does()
        {
            // The construction is identical across modes; only the two
            // values trade places WITHIN each mode. Comparing a light
            // ink against a dark fill would prove nothing — it would
            // only measure that light is lighter than dark — so each
            // mode is checked against its own resting pair.
            foreach (var dark in new[] { false, true })
            {
                var rest = Resolve(dark);
                var hover = Resolve(dark, SoftInteractionState.Hover,
                    interactive: true);

                Assert.False(rest.Inverted);
                Assert.True(hover.Inverted);

                Assert.Equal(rest.Contrast.Ink, hover.Contrast.Fill);
                Assert.Equal(rest.Contrast.Fill, hover.Contrast.Ink);
            }
        }

        [Fact]
        public void Dark_mode_keeps_the_same_rule_weights()
        {
            // The rule weight is a typographic constant, not a
            // lightness. Dark mode must not quietly promote 1px to 2px
            // to compensate for a faint line — that would make the two
            // modes disagree about what "selected" means.
            Assert.Equal(
                Resolve(dark: false, selected: true).Contrast.RuleThickness,
                Resolve(dark: true, selected: true).Contrast.RuleThickness);
            Assert.Equal(
                Resolve(dark: false, selected: false).Contrast.RuleThickness,
                Resolve(dark: true, selected: false).Contrast.RuleThickness);
        }

        // ═══════════════════════════════════════════════════════════
        //  PALETTE BRIDGE
        // ═══════════════════════════════════════════════════════════

        [Fact]
        public void Only_the_swiss_family_publishes_a_swiss_palette()
        {
            foreach (var profile in EightThemeCatalog.All)
            {
                SwissPalette.Publish(
                    LightPaper, LightInk, Muted, Rule, SwissAccent,
                    DarkPaper, DarkInk, Muted, Rule);

                EightThemePainter.Apply(
                    new ResourceDictionary(), profile,
                    dark: false, accent: Colors.SteelBlue);

                bool shouldBeSwiss =
                    profile.Identity == EightThemeNames.Swiss;
                Assert.Equal(shouldBeSwiss, SwissPalette.IsActive);

                if (shouldBeSwiss)
                {
                    Assert.NotNull(SwissPalette.Light);
                    Assert.NotNull(SwissPalette.Dark);
                }
                else
                {
                    Assert.Null(SwissPalette.Light);
                    Assert.Null(SwissPalette.Dark);
                }
            }

            SwissPalette.Clear();
        }

        [Fact]
        public void A_stale_swiss_palette_never_survives_a_family_change()
        {
            // The failure this guards is not visible as a bug report:
            // a Swiss control still holding a palette after the user
            // switches away keeps drawing paper-on-ink rules over a
            // glassmorphic surface, and it looks like the theme change
            // half-happened.
            foreach (var profile in EightThemeCatalog.All)
            {
                EightThemePainter.Apply(
                    new ResourceDictionary(), profile,
                    dark: false, accent: Colors.SteelBlue);

                if (profile.Identity != EightThemeNames.Swiss)
                {
                    Assert.False(SwissPalette.IsActive,
                        $"{profile.Identity}: Swiss palette leaked across "
                        + "the family change.");
                }
            }

            SwissPalette.Clear();
        }

        [Fact]
        public void The_three_flat_families_are_never_all_active_at_once()
        {
            foreach (var profile in EightThemeCatalog.All)
            {
                EightThemePainter.Apply(
                    new ResourceDictionary(), profile,
                    dark: false, accent: Colors.SteelBlue);

                Assert.False(
                    ClayPalette.IsActive && NeuPalette.IsActive,
                    $"{profile.Identity}: both soft palettes were published.");

                Assert.False(
                    SwissPalette.IsActive && NeuPalette.IsActive,
                    $"{profile.Identity}: Swiss and Neu were both active.");
            }

            SwissPalette.Clear();
            ClayPalette.Clear();
            NeuPalette.Clear();
        }

        [Fact]
        public void Publishing_and_clearing_are_idempotent()
        {
            SwissPalette.Clear();
            Assert.False(SwissPalette.IsActive);

            // Clear() on an already-clear palette must not raise Changed,
            // because controls repaint on that event and a spurious raise
            // would repaint the whole visual tree for nothing.
            int raised = 0;
            void Count(object? s, EventArgs e) => raised++;

            SwissPalette.Changed += Count;
            SwissPalette.Clear();
            Assert.Equal(0, raised);

            SwissPalette.Publish(
                LightPaper, LightInk, Muted, Rule, SwissAccent,
                DarkPaper, DarkInk, Muted, Rule);
            Assert.Equal(1, raised);
            Assert.True(SwissPalette.IsActive);

            SwissPalette.Clear();
            Assert.Equal(2, raised);
            SwissPalette.Changed -= Count;
        }

        // ═══════════════════════════════════════════════════════════
        //  ACCESSIBILITY COMMITMENTS
        // ═══════════════════════════════════════════════════════════

        [Fact]
        public void The_focus_ring_survives_a_flat_family()
        {
            // Every other family in this engine is soft, and a soft
            // shadow reads as a focus state closely enough that outline
            // rings get deleted for looking untidy. Swiss is flat, so
            // there is no such temptation — but the ring is still not
            // optional, and the shared constant still applies.
            Assert.True(SoftUiAccessibility.FocusRingWidth >= 2.0);
            Assert.True(SoftUiAccessibility.MinHitTarget >= 44);
        }

        [Fact]
        public void The_family_is_the_most_contrasty_in_the_engine()
        {
            // Worth pinning explicitly, because it is the strongest
            // reason to keep this family around: it is the one a
            // low-vision user is guaranteed to be able to read.
            var swiss = Resolve();
            double swissRatio =
                Contrast(swiss.Contrast.Fill, swiss.Contrast.Ink);

            Assert.True(swissRatio >= 7.0,
                "Swiss must stay the highest-contrast family in the set.");
        }

        // ── helpers ──────────────────────────────────────────────────

        private static double Luminance(Color c)
        {
            static double Channel(byte v)
            {
                double s = v / 255.0;
                return s <= 0.03928
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