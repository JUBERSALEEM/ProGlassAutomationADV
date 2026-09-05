// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/CrystalFluidControlsSourceTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// IOS-17PM P2 — controls-plane fix-register pins (2026-09-05).
//
// The authored Ios-17pm control layer (7 code files + 5 template
// dictionaries) shipped with 33 catalogued analysis findings. P2
// integrated the layer applying every applicable fix. These tests pin
// the fixes so a future "sync with the authored source" cannot silently
// re-import a catalogued defect, and they pin the purely-additive
// contract (nothing existing changes until P4 adopts hosts).
//
// Register ids are cited per assertion for traceability.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Tests.TestSupport;
using System;
using System.IO;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public sealed class CrystalFluidControlsSourceTests
    {
        private static string Read(string relative) =>
            File.ReadAllText(Path.Combine(LocateProjectRoot(),
                relative.Replace('/', Path.DirectorySeparatorChar)));

        // Defect-pin assertions run against CODE, never comments — the
        // file banners cite the authored defect patterns they removed, so
        // the audit strings must not trip the pins.
        private static string StripXmlComments(string s) =>
            System.Text.RegularExpressions.Regex.Replace(
                s, "<!--.*?-->", string.Empty,
                System.Text.RegularExpressions.RegexOptions.Singleline);

        private static string StripCsLineComments(string s) =>
            System.Text.RegularExpressions.Regex.Replace(
                s, "(?m)^\\s*//.*$", string.Empty);

        private static string LocateProjectRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "ProGlassAutomation.csproj")))
                    return dir.FullName;
                dir = dir.Parent;
            }
            throw new FileNotFoundException("Project root not found");
        }

        private const string Water =
            "Controls/CrystalFluid/Controls/CrystalWaterControl.cs";
        private const string Card =
            "Controls/CrystalFluid/Controls/CrystalCard.cs";
        private const string Kpi =
            "Controls/CrystalFluid/Controls/CrystalKpiCard.cs";
        private const string Pill =
            "Controls/CrystalFluid/Controls/CrystalStatusPill.cs";
        private const string Progress =
            "Controls/CrystalFluid/Controls/CrystalProgressBar.cs";
        private const string Ambient =
            "Controls/CrystalFluid/Controls/CrystalAmbientBackground.cs";
        private const string Converter =
            "Controls/CrystalFluid/Converters/ProgressWidthConverter.cs";

        private const string ControlsDict =
            "Themes/CrystalFluid/CrystalFluidControls.xaml";
        private const string StylesDict =
            "Themes/CrystalFluid/CrystalFluidStyles.xaml";
        private const string NavDict =
            "Themes/CrystalFluid/CrystalFluidNavigation.xaml";
        private const string GridDict =
            "Themes/CrystalFluid/CrystalFluidDataGrid.xaml";
        private const string ScrollDict =
            "Themes/CrystalFluid/CrystalFluidScrollBar.xaml";

        // ── D1: storyboards are controllable; Stop never throws ─────────
        [Fact]
        public void D1_water_storyboards_use_controllable_begin_stop()
        {
            string cs = StripCsLineComments(Read(Water));

            Assert.Contains("Begin(this, true)", cs, StringComparison.Ordinal);
            Assert.Contains("Stop(this)", cs, StringComparison.Ordinal);
            // the authored bare forms are the crash class — gone for good
            Assert.DoesNotContain(".Begin();", cs, StringComparison.Ordinal);
            Assert.DoesNotContain("sb.Stop();", cs, StringComparison.Ordinal);
            Assert.DoesNotContain("_waveStoryboard?.Stop();", cs,
                StringComparison.Ordinal);
            Assert.DoesNotContain("_causticStoryboard?.Stop();", cs,
                StringComparison.Ordinal);
        }

        // ── Template-part contract (D14 dead PART purged) ───────────────
        [Fact]
        public void Template_parts_match_code_and_dead_part_is_gone()
        {
            string xaml = StripXmlComments(Read(ControlsDict));
            string cs = StripCsLineComments(Read(Water));

            foreach (var part in new[]
                { "PART_WaterCanvas", "PART_WaterBody",
                  "PART_CausticLayer", "PART_WavePath" })
            {
                Assert.Contains($"x:Name=\"{part}\"", xaml,
                    StringComparison.Ordinal);
                Assert.Contains($"GetTemplateChild(\"{part}\")", cs,
                    StringComparison.Ordinal);
            }

            Assert.DoesNotContain("PART_SurfaceLine", cs,
                StringComparison.Ordinal);
        }

        // ── D7: wave is anchored to the waterline ───────────────────────
        [Fact]
        public void D7_wave_rides_the_waterline_not_the_card_top()
        {
            string xaml = StripXmlComments(Read(ControlsDict));
            string cs = Read(Water);

            // authored hardcode pinned the wave at the card top
            Assert.DoesNotContain("Canvas.Top=\"0\"", xaml,
                StringComparison.Ordinal);
            Assert.Contains("WaveTopForCurrentLevel", cs,
                StringComparison.Ordinal);
            Assert.Contains("Canvas.SetTop(_wavePath", cs,
                StringComparison.Ordinal);
        }

        // ── D8: card header and content no longer overlap ───────────────
        [Fact]
        public void D8_card_template_docks_header_above_content()
        {
            string xaml = StripXmlComments(Read(ControlsDict));

            int dockPanel = xaml.IndexOf("<DockPanel Panel.ZIndex=\"5\">",
                StringComparison.Ordinal);
            int header = xaml.IndexOf("{TemplateBinding Header}",
                StringComparison.Ordinal);
            int content = xaml.IndexOf("Margin=\"{TemplateBinding Padding}\"",
                StringComparison.Ordinal);

            Assert.True(dockPanel > 0, "Card content must sit in a DockPanel.");
            Assert.True(header > dockPanel && content > header,
                "Header presenter must dock above the content presenter.");
        }

        // ── D12: KPI trend API is consumed by the template ──────────────
        [Fact]
        public void D12_kpi_trend_is_live_in_template()
        {
            string xaml = StripXmlComments(Read(ControlsDict));

            Assert.Contains("{TemplateBinding TrendText}", xaml,
                StringComparison.Ordinal);
            Assert.Contains("TrendPositive", xaml, StringComparison.Ordinal);
            Assert.Contains("CrystalDangerBrush", xaml,
                StringComparison.Ordinal);
        }

        // ── D24/D25/D26: buttons & combo fixes ──────────────────────────
        [Fact]
        public void D24_primary_button_template_consumes_its_hover_brush()
        {
            string xaml = StripXmlComments(Read(StylesDict));

            Assert.Contains("CrystalPrimaryButtonHoverBrush", xaml,
                StringComparison.Ordinal);
            Assert.Contains("x:Key=\"CrystalPrimaryButtonStyle\"", xaml,
                StringComparison.Ordinal);
        }

        [Fact]
        public void D25_no_template_null_shadow_and_D26_items_are_styled()
        {
            string xaml = StripXmlComments(Read(StylesDict));

            Assert.DoesNotContain("Template=\"{x:Null}\"", xaml,
                StringComparison.Ordinal);
            Assert.Contains("ItemContainerStyle", xaml,
                StringComparison.Ordinal);
            Assert.Contains("CrystalComboBoxItemStyle", xaml,
                StringComparison.Ordinal);
        }

        // ── D27/D28: scroll chrome keeps the standard contract ──────────
        [Fact]
        public void D27_scroll_content_presenter_binds_can_content_scroll()
        {
            string xaml = StripXmlComments(Read(ScrollDict));

            Assert.Contains("CanContentScroll=\"{TemplateBinding", xaml,
                StringComparison.Ordinal);
        }

        [Fact]
        public void D28_track_direction_is_not_reversed()
        {
            string xaml = StripXmlComments(Read(ScrollDict));

            Assert.DoesNotContain("IsDirectionReversed", xaml,
                StringComparison.Ordinal);
        }

        // ── Virtualization law (untouched by the visual layer) ──────────
        [Fact]
        public void Datagrid_style_preserves_virtualization_recycling()
        {
            string xaml = StripXmlComments(Read(GridDict));

            Assert.Contains("VirtualizingPanel.IsVirtualizing", xaml,
                StringComparison.Ordinal);
            Assert.Contains("VirtualizingPanel.VirtualizationMode", xaml,
                StringComparison.Ordinal);
            Assert.Contains("Recycling", xaml, StringComparison.Ordinal);
            Assert.Contains("EnableRowVirtualization", xaml,
                StringComparison.Ordinal);
            Assert.Contains("EnableColumnVirtualization", xaml,
                StringComparison.Ordinal);
        }

        // ── D11: pill default is the authored positive, glow follows ────
        [Fact]
        public void D11_pill_default_and_glow_follow_status_color()
        {
            string cs = StripCsLineComments(Read(Pill));
            string xaml = StripXmlComments(Read(ControlsDict));

            Assert.DoesNotContain("LimeGreen", cs, StringComparison.Ordinal);
            Assert.Contains("{DynamicResource CrystalPositiveBrush}", xaml,
                StringComparison.Ordinal);
            Assert.Contains("StatusColor.(SolidColorBrush.Color)", xaml,
                StringComparison.Ordinal);
        }

        // ── D5: reduced-motion defaults from the token (live link) ──────
        [Fact]
        public void D5_reduced_motion_defaults_are_token_fed()
        {
            string xaml = StripXmlComments(Read(ControlsDict));
            string ambient = StripCsLineComments(Read(Ambient));

            Assert.Contains("Value=\"{DynamicResource CrystalReducedMotion}\"",
                xaml, StringComparison.Ordinal);
            Assert.Contains("SetResourceReference", ambient,
                StringComparison.Ordinal);
        }

        // ── D9: ambient clocks stop on unload ───────────────────────────
        [Fact]
        public void D9_ambient_unload_stops_the_clocks()
        {
            string cs = Read(Ambient);

            int handler = cs.IndexOf("private void OnUnloaded",
                StringComparison.Ordinal);
            Assert.True(handler > 0);
            int window = cs.IndexOf("StopAnimation();", handler,
                StringComparison.Ordinal);
            Assert.True(window > handler && window - handler < 220,
                "OnUnloaded must stop the blob storyboards.");
        }

        // ── D6/D15: coerces are wired into DP metadata; amplitude is live ──
        [Fact]
        public void D6_coerces_actually_hook_the_metadata()
        {
            string cs = StripCsLineComments(Read(Water));

            // a defined-but-unwired coerce is a silent no-fix (integration
            // review caught exactly that) — pin the metadata wiring
            Assert.Contains("new PropertyMetadata(1.0, null, CoerceWaterSpeed)",
                cs, StringComparison.Ordinal);
            Assert.DoesNotContain("ValidateNonNegative", cs,
                StringComparison.Ordinal);
        }

        [Fact]
        public void D15_wave_animation_consumes_the_amplitude_dp()
        {
            string cs = Read(Water);

            Assert.Contains("double amp = WaveAmplitude;", cs,
                StringComparison.Ordinal);
            Assert.Contains("From = -amp", cs, StringComparison.Ordinal);
            Assert.Contains("To = amp", cs, StringComparison.Ordinal);
        }

        // ── D4: water opacity axis exists and reaches the canvas ────────
        [Fact]
        public void D4_water_opacity_is_a_live_axis()
        {
            string cs = Read(Water);
            string xaml = Read(ControlsDict);

            Assert.Contains("WaterOpacityProperty", cs,
                StringComparison.Ordinal);
            Assert.Contains("Opacity=\"{TemplateBinding WaterOpacity}\"",
                xaml, StringComparison.Ordinal);
        }

        // ── Namespace plane: authored names land under ProGlassAutomation
        [Fact]
        public void Controls_use_the_app_namespace()
        {
            foreach (var file in new[] { Water, Card, Kpi, Pill, Progress, Ambient, Converter })
            {
                string cs = Read(file);
                Assert.Contains("namespace ProGlassAutomation.Controls.CrystalFluid",
                    cs, StringComparison.Ordinal);
                Assert.DoesNotContain("namespace CrystalFluid.Controls",
                    cs, StringComparison.Ordinal);
            }

            Assert.Contains(
                "xmlns:crystal=\"clr-namespace:ProGlassAutomation.Controls.CrystalFluid\"",
                Read(ControlsDict), StringComparison.Ordinal);
        }

        // ── App.xaml: the five template dictionaries merge in authored
        //    order after the token plane ─────────────────────────────────
        [Fact]
        public void App_xaml_merges_template_dictionaries_after_tokens()
        {
            string app = Read("App.xaml");

            int tokens = app.IndexOf(
                "Themes/CrystalFluid/CrystalFluidTokens.xaml",
                StringComparison.Ordinal);
            int controls = app.IndexOf(
                "Themes/CrystalFluid/CrystalFluidControls.xaml",
                StringComparison.Ordinal);
            int styles = app.IndexOf(
                "Themes/CrystalFluid/CrystalFluidStyles.xaml",
                StringComparison.Ordinal);
            int nav = app.IndexOf(
                "Themes/CrystalFluid/CrystalFluidNavigation.xaml",
                StringComparison.Ordinal);
            int grid = app.IndexOf(
                "Themes/CrystalFluid/CrystalFluidDataGrid.xaml",
                StringComparison.Ordinal);
            int scroll = app.IndexOf(
                "Themes/CrystalFluid/CrystalFluidScrollBar.xaml",
                StringComparison.Ordinal);

            Assert.True(tokens > 0 && controls > tokens &&
                        styles > controls && nav > styles &&
                        grid > nav && scroll > grid,
                "Authored master order: tokens → controls → styles → " +
                "navigation → datagrid → scrollbar.");
        }

        // ── Integration nouns exist and literal bypasses are tokenized ──
        [Fact]
        public void Integration_nouns_back_every_template_usage()
        {
            string metrics = Read("Themes/CrystalFluid/CrystalFluidMetrics.xaml");
            string brushes = Read("Themes/CrystalFluid/CrystalFluidBrushes.xaml");
            string controls = StripXmlComments(Read(ControlsDict));
            string styles = StripXmlComments(Read(StylesDict));
            string scroll = StripXmlComments(Read(ScrollDict));
            string grid = StripXmlComments(Read(GridDict));

            foreach (var k in new[]
            {
                "CrystalOpacityDisabled", "CrystalGridDataRowHeight",
                "CrystalGridHeaderHeight", "CrystalScrollBarSize",
                "CrystalRadiusScrollBar",
            })
                Assert.Contains($"x:Key=\"{k}\"", metrics,
                    StringComparison.Ordinal);

            Assert.Contains("x:Key=\"CrystalHighlightBrush\"", brushes,
                StringComparison.Ordinal);
            Assert.Contains("x:Key=\"CrystalPopupBackgroundBrush\"", brushes,
                StringComparison.Ordinal);

            // the authored literal bypasses must not survive integration
            Assert.DoesNotContain("#D9FFFFFF", controls,
                StringComparison.Ordinal);
            Assert.DoesNotContain("#B8FFFFFF", controls,
                StringComparison.Ordinal);
            Assert.DoesNotContain("#F0F5FAFF", styles,
                StringComparison.Ordinal);
            Assert.DoesNotContain("Fill=\"#D9FFFFFF\"", styles,
                StringComparison.Ordinal);

            // tokenized usages are present
            Assert.Contains("CrystalHighlightBrush", controls,
                StringComparison.Ordinal);
            Assert.Contains("CrystalHighlightBrush", styles,
                StringComparison.Ordinal);
            Assert.Contains("CrystalRadiusScrollBar", scroll,
                StringComparison.Ordinal);
            Assert.Contains("CrystalGridDataRowHeight", grid,
                StringComparison.Ordinal);
        }

        // ── Converter math (file 11 contract: fraction, clamp, guards) ──
        [Fact]
        public void ProgressWidthConverter_computes_clamped_fraction()
        {
            var conv =
                new ProGlassAutomation.Controls.CrystalFluid
                    .ProgressWidthConverter();
            var culture = System.Globalization.CultureInfo.InvariantCulture;

            double Mid() => (double)conv.Convert(
                new object[] { 50.0, 0.0, 100.0, 200.0 },
                typeof(double), null, culture);

            Assert.Equal(100.0, Mid());

            // fraction clamps to [0,1]
            Assert.Equal(0.0, (double)conv.Convert(
                new object[] { -5.0, 0.0, 100.0, 200.0 },
                typeof(double), null, culture));
            Assert.Equal(200.0, (double)conv.Convert(
                new object[] { 500.0, 0.0, 100.0, 200.0 },
                typeof(double), null, culture));

            // min == max (or inverted range) yields zero width, never NaN
            Assert.Equal(0.0, (double)conv.Convert(
                new object[] { 50.0, 100.0, 100.0, 200.0 },
                typeof(double), null, culture));

            // non-double inputs (UnsetValue at parse time) are safe
            Assert.Equal(0.0, (double)conv.Convert(
                new object[] { "unset", 0.0, 100.0, 200.0 },
                typeof(double), null, culture));
            Assert.Equal(0.0, (double)conv.Convert(
                new object[] { 50.0 }, typeof(double), null, culture));
        }
    }
}
