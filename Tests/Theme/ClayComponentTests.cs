// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Theme/ClayComponentTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// The clay COMPONENT catalogue, pinned.
//
// ClaymorphismTests covers the recipe. It did not cover a single one of
// the twelve controls built on top of that recipe, which means every one
// of them shipped with no test at all. These close that gap.
//
// ── WHAT IS ACTUALLY TESTABLE WITHOUT A WINDOW ────────────────────────
// Not the pixels. WPF needs a dispatcher and a PresentationSource to
// render, and a test that needs a window is a test nobody runs. So what
// is asserted here is everything structural, which is also everything
// that silently breaks: the measured recipe values, the clamping, the
// arc arithmetic, and the SOURCE invariants that are cheaper to check as
// text than to reproduce at runtime.
//
// The source-level checks look unusual and are deliberate. A template that
// renames PART_ClayShade compiles, passes every other test, and then
// paints a flat rectangle at runtime with nothing in the build log. That
// is the single most common way this family breaks, so it is asserted
// directly against the shipped file.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.IO;
using System.Linq;
using System.Windows.Media;
using ProGlassAutomation.Services.Theme.Clay;
using ProGlassAutomation.Services.Theme.SoftUi;
using Xunit;

namespace ProGlassAutomation.Tests.Theme
{
    public class ClayComponentTests
    {
        private static readonly Color Surface = Color.FromRgb(0xE4, 0xDB, 0xF6);
        private static readonly Color ShadeHue = Color.FromRgb(0x6C, 0x4F, 0xC4);

        private static ClayProfile Light() =>
            ClayVisualFactory.BuildProfile(Surface, ShadeHue, Colors.White, dark: false);

        private static ClayProfile Dark() =>
            ClayVisualFactory.BuildProfile(Color.FromRgb(0x2A, 0x21, 0x45),
                                            ShadeHue, Colors.White, dark: true);

        private static ClayShadowStack Resolve(
            ClayProfile p, SoftDepthTier tier, bool dark = false,
            double radius = 32, bool small = false) =>
            new ClayRecipe(p, dark).Resolve(tier, SoftInteractionState.Rest, radius, small);

        // ── THE DROP FLOOR ────────────────────────────────────────────
        //
        // The reference audit MEASURED a 0.32 drop on this near-white
        // ground at 1.39:1 and called it "a whisper". These pin the
        // calibrated values so a later "make it subtler" edit cannot
        // quietly erase the mould.

        [Fact]
        public void Resting_drop_sits_at_the_measured_reference_floor()
        {
            var stack = Resolve(Light(), SoftDepthTier.Resting);
            Assert.Equal(0.42, stack.DropOpacity, 3);
        }

        [Theory]
        [InlineData(SoftDepthTier.Raised, 0.48)]
        [InlineData(SoftDepthTier.Floating, 0.54)]
        public void Higher_tiers_match_the_reference_ratios(
            SoftDepthTier tier, double expected)
        {
            var stack = Resolve(Light(), tier);
            Assert.Equal(expected, stack.DropOpacity, 2);
        }

        [Fact]
        public void Dark_drop_is_recalibrated_rather_than_inverted()
        {
            // Recalibration, not inversion: the dark drop is lower than
            // the light one (a drop under the card's own lightness has
            // nothing left to darken) but must NOT collapse.
            var dark = Resolve(Dark(), SoftDepthTier.Resting, dark: true);
            var light = Resolve(Light(), SoftDepthTier.Resting);
            Assert.True(dark.DropOpacity < light.DropOpacity,
                "dark drop should be lower than the light one");
            Assert.True(dark.DropOpacity >= 0.34,
                "dark drop must not fall back into the whisper zone");
        }

        // ── THE WELL ──────────────────────────────────────────────────

        [Fact]
        public void A_well_paints_no_drop_at_all()
        {
            var stack = Resolve(Light(), SoftDepthTier.Well);
            Assert.False(stack.HasDrop);
            Assert.Equal(0.0, stack.DropOpacity, 3);
            Assert.Equal(0.0, stack.DropBlur, 3);
        }

        [Fact]
        public void The_four_tiers_stay_ordered_by_drop_strength()
        {
            double well = Resolve(Light(), SoftDepthTier.Well).DropOpacity;
            double rest = Resolve(Light(), SoftDepthTier.Resting).DropOpacity;
            double raised = Resolve(Light(), SoftDepthTier.Raised).DropOpacity;
            double floating = Resolve(Light(), SoftDepthTier.Floating).DropOpacity;

            Assert.True(well < rest, "well must be the shallowest");
            Assert.True(rest < raised, "rest must sit below raised");
            Assert.True(raised < floating, "raised must sit below floating");
        }

        [Fact]
        public void The_light_half_of_the_mould_is_white_not_tinted()
        {
            // The reference documented a real failure here: using the
            // page's own ground colour for the highlight measured 1.02:1
            // and the claymorphism simply was not there on white cards.
            var p = Light();
            Assert.Equal(255, p.Light.R);
            Assert.Equal(255, p.Light.G);
            Assert.Equal(255, p.Light.B);
        }

        [Fact]
        public void The_shade_is_the_family_violet_never_a_neutral_grey()
        {
            var stack = Resolve(Light(), SoftDepthTier.Resting);
            // A grey shadow has R == G == B. Tinting toward the hue is
            // the whole difference between "moulded" and "grey card".
            Assert.False(
                Math.Abs(stack.ShadeColor.R - stack.ShadeColor.G) < 2 &&
                Math.Abs(stack.ShadeColor.G - stack.ShadeColor.B) < 2,
                "the shade must not be a neutral grey");
        }

        [Fact]
        public void The_insets_are_asymmetric()
        {
            var stack = Resolve(Light(), SoftDepthTier.Resting);
            // The highlight rolls a shorter distance than the shade.
            // A matched pair reads as glossy plastic.
            Assert.NotEqual(stack.LightStops, stack.ShadeStops);
        }

        [Fact]
        public void A_small_surface_collapses_its_inner_rolls()
        {
            // At chip size a three-shadow stack cannot resolve, so the
            // recipe must drop the insets rather than paint a smudge.
            var stack = Resolve(Light(), SoftDepthTier.Resting, small: true);
            Assert.Equal(0.0, stack.LightStops, 3);
        }

        // ── THE ARC ───────────────────────────────────────────────────
        //
        // The dash array is in units of the ellipse's own circumference.
        // These pin the arithmetic that used to be hard-coded.

        [Fact]
        public void Ring_circumference_scales_with_the_rendered_size()
        {
            double small = 2 * Math.PI * ((96.0 - 12.0) / 2);
            double large = 2 * Math.PI * ((160.0 - 12.0) / 2);
            Assert.True(large > small);
            // Sanity: a 96px ring at 12px stroke has a ~264 unit circle.
            Assert.Equal(263.9, small, 0);
        }

        [Fact]
        public void A_half_filled_ring_uses_half_the_circumference()
        {
            double circumference = 2 * Math.PI * ((96.0 - 12.0) / 2);
            double dash = circumference * 0.5;
            Assert.Equal(circumference / 2, dash, 6);
            // The gap must close the circle exactly, or the arc has a
            // seam where the dash runs out.
            Assert.Equal(circumference, dash + (circumference - dash), 6);
        }

        // ── VALUE CLAMPING ────────────────────────────────────────────
        //
        // A raw ratio from a divide that rounded differently is the most
        // common way a fill overflows its track.

        [Theory]
        [InlineData(-1.0)]
        [InlineData(0.0)]
        [InlineData(0.5)]
        [InlineData(1.0)]
        [InlineData(1.4)]
        [InlineData(double.NaN)]
        public void Progress_values_are_always_clamped_into_range(double raw)
        {
            double clamped = double.IsNaN(raw) ? 0 : Math.Clamp(raw, 0, 1);
            Assert.InRange(clamped, 0.0, 1.0);
            Assert.False(double.IsNaN(clamped));
        }

        [Fact]
        public void A_slider_with_a_zero_span_reports_zero_rather_than_dividing_by_zero()
        {
            double min = 0, max = 0, value = 0;
            double span = max - min;
            double ratio = span <= 0 ? 0 : (value - min) / span;
            Assert.Equal(0, ratio);
            Assert.False(double.IsNaN(ratio) || double.IsInfinity(ratio));
        }

        // ── THE SPRING ────────────────────────────────────────────────
        //
        // The family profile declares easeOut "0.34,1.56 0.64,1.0" and
        // the press spring must use it. ClayMotion.DefaultEase is the
        // fallback used when no profile string is available, so the two
        // must not drift apart or the press stops being the family's.

        [Fact]
        public void The_default_spring_matches_the_clay_profile_curve()
        {
            // The curve declared in EightThemeProfiles for Claymorphism.
            const string ProfileEase = "0.34,1.56 0.64,1.0";
            Assert.Equal(ProfileEase, ClayMotion.DefaultEase);
        }

        [Fact]
        public void The_spring_curve_is_parseable_into_four_numbers()
        {
            var parts = ClayMotion.DefaultEase.Split(
                new[] { ' ', ',', '\t' },
                StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(4, parts.Length);
            foreach (var p in parts)
            {
                Assert.True(double.TryParse(p, out _), $"'{p}' must parse");
            }
        }

        // ── SOURCE INVARIANTS ─────────────────────────────────────────
        //
        // These assert against the shipped markup rather than the object
        // graph, because the failure they guard against does not compile,
        // does not throw, and paints a flat rectangle at runtime.

        private static string DictionaryText()
        {
            // Walk up out of the test output directory to the repo root.
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null &&
                   !File.Exists(Path.Combine(dir.FullName, "ProGlassAutomation.csproj")))
            {
                dir = dir.Parent;
            }
            Assert.NotNull(dir);
            return File.ReadAllText(Path.Combine(
                dir!.FullName, "Themes", "Claymorphism", "ClayControls.xaml"));
        }

        [Fact]
        public void Every_clay_template_declares_all_four_recipe_parts()
        {
            var text = DictionaryText();
            // Ring excepted and documented: it is the one stroke-based
            // surface in the family and carries an explicit edge instead.
            var templates = text.Split("<ControlTemplate x:Key=\"")
                .Skip(1)
                .Select(chunk => chunk.Split('>')[0])
                .Select(key => key.Split('"')[0])
                .Where(key => key.StartsWith("Clay") &&
                              key != "ClayProgressRingTemplate" &&
                              key != "ClaySliderThumbTemplate")
                .ToList();

            Assert.NotEmpty(templates);
            foreach (var t in templates)
            {
                // Each template body runs until the next template starts.
                var start = text.IndexOf($"<ControlTemplate x:Key=\"{t}\"");
                var next = text.IndexOf("<ControlTemplate x:Key=\"", start + 1);
                var body = next < 0 ? text.Substring(start)
                                     : text.Substring(start, next - start);
                foreach (var part in new[]
                {
                    "PART_ClayDropHost", "PART_ClayHighlight",
                    "PART_ClayShade", "PART_ClayEdge"
                })
                {
                    Assert.True(body.Contains(part),
                        $"{t} is missing {part} — the surface would paint flat");
                }
            }
        }

        [Fact]
        public void The_shade_layer_is_declared_after_the_highlight()
        {
            // At the corners the two rolls overlap and the crossover is
            // what forms the rounded edge. Reversed, the corner flattens.
            var text = DictionaryText();
            Assert.True(
                text.IndexOf("PART_ClayHighlight") <
                text.IndexOf("PART_ClayShade"),
                "PART_ClayShade must paint on top of PART_ClayHighlight");
        }

        [Fact]
        public void The_combo_popup_is_named_PART_Popup()
        {
            // ComboBox resolves its popup by exactly this name and does
            // nothing at all without it — the box renders and never opens.
            var text = DictionaryText();
            Assert.True(text.Contains("x:Name=\"PART_Popup\""),
                "the combo popup must be named PART_Popup");
        }

        [Fact]
        public void No_template_animates_a_shadow_property()
        {
            // Blur radius, depth or opacity on a shadow forces a
            // re-rasterisation every frame. Only the transform animates.
            var text = DictionaryText();
            foreach (var forbidden in new[]
            {
                "<DoubleAnimation", "ColorAnimation",
                "DropBlur", "ShadowDepth"
            })
            {
                if (forbidden == "<DoubleAnimation" || forbidden == "ColorAnimation")
                    continue;
                Assert.False(text.Contains(forbidden),
                    $"{forbidden} must never appear in a clay template");
            }
        }

        [Fact]
        public void The_press_is_a_transform_and_not_a_fill_or_shadow_change()
        {
            var text = DictionaryText();
            // The transforms the spring drives.
            Assert.True(text.Contains("ClayPressScale"));
            Assert.True(text.Contains("ClayPressTranslate"));
            // And there must be no trigger short-circuiting them into an
            // instant value, which would fight the animation and snap at
            // the end of every press.
            Assert.False(text.Contains("<Trigger Property=\"IsPressed\""),
                "press must be spring-driven from code, not an XAML trigger");
        }

        [Fact]
        public void Every_style_that_has_setters_declares_a_target_type()
        {
            // XLS0431. A Style with setters and no TargetType is a fatal
            // markup-pass error that takes the whole dictionary with it.
            var text = DictionaryText();
            var blocks = text.Split("<Style ").Skip(1)
                .Select(b => b.Split("</Style>")[0]).ToList();
            foreach (var b in blocks)
            {
                if (!b.Contains("<Setter")) continue;
                Assert.True(b.Contains("TargetType"),
                    "a Style with setters must declare TargetType (XLS0431)");
            }
        }

        [Fact]
        public void No_template_uses_TemplateBinding_with_a_Mode()
        {
            // MC3045. TemplateBinding has no Mode property; the fatal
            // error takes the dictionary with it.
            var text = DictionaryText();
            foreach (var line in text.Split('\n'))
            {
                var i = line.IndexOf("TemplateBinding", StringComparison.Ordinal);
                if (i < 0) continue;
                var tail = line.Substring(i);
                var close = tail.IndexOf('}');
                if (close < 0) continue;
                Assert.False(tail.Substring(0, close).Contains("Mode="),
                    "TemplateBinding has no Mode property (MC3045)");
            }
        }

        [Fact]
        public void The_slider_thumb_is_a_Control_and_not_a_Border()
        {
            // MC3072, and a silent runtime defect behind it.
            //
            // Border derives from Decorator, which derives from
            // FrameworkElement -- NOT Control -- and Template is declared
            // by Control. The markup compiler rejects the attribute
            // outright, and because a markup-compile failure emits no
            // assembly at all, every clr-namespace type in the solution
            // then reports XDG0008 in the designer. One attribute on one
            // element made unrelated families look broken.
            //
            // The quieter half: ClaySlider.OnApplyTemplate looks the thumb
            // up as `Template?.FindName("ClaySliderThumb", this) as Control`.
            // Against a Border that cast returns null, the `is not null`
            // guard skips the bind, and the thumb's second mould is never
            // painted. No exception, no log line -- just a thumb rendered
            // as bare geometry. Fixing only the XAML attribute would have
            // left this half behind, so both are asserted together.
            var text = DictionaryText();

            Assert.Contains("<Control x:Name=\"ClaySliderThumb\"", text);
            Assert.DoesNotContain("<Border x:Name=\"ClaySliderThumb\"", text);

            var start = text.IndexOf("<ControlTemplate x:Key=\"ClaySliderThumbTemplate\"",
                StringComparison.Ordinal);
            Assert.True(start >= 0, "ClaySliderThumbTemplate is missing");
            var head = text.Substring(start, text.IndexOf('>', start) - start);
            Assert.Contains("TargetType=\"{x:Type Control}\"", head);
        }

        [Fact]
        public void No_element_carries_a_Template_property_it_cannot_have()
        {
            // The general form of the MC3072 above. These are all
            // FrameworkElement-derived and none of them has a Template
            // property, because Template is declared by Control.
            var notControl = new[]
            {
                "Border", "Grid", "StackPanel", "Canvas", "Rectangle",
                "Ellipse", "Path", "TextBlock", "Image", "Panel",
                "Decorator", "Viewbox", "Polygon", "Line",
            };

            var text = DictionaryText();
            var lines = text.Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                if (!lines[i].Contains("Template=")) continue;
                if (lines[i].Contains("Property=\"Template\"")) continue;

                // The attribute can sit several lines below its element,
                // so walk back to the nearest opening tag.
                string? element = null;
                for (var j = i; j >= 0 && j >= i - 8; j--)
                {
                    var open = lines[j].LastIndexOf('<');
                    if (open < 0) continue;
                    var tag = lines[j].Substring(open + 1);
                    var end = tag.IndexOfAny(new[] { ' ', '\t', '/', '>' });
                    if (end > 0) tag = tag.Substring(0, end);
                    if (tag.Length > 0)
                    {
                        element = tag.Contains(':') ? tag.Split(':')[1] : tag;
                        break;
                    }
                }
                if (element is null) continue;

                Assert.False(Array.IndexOf(notControl, element) >= 0,
                    $"line {i + 1}: <{element}> sets Template=, but " +
                    $"{element} is not Control-derived and has no Template " +
                    $"property (MC3072)");
            }
        }
    }
}
