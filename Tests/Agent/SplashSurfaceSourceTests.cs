using System;
using System.IO;
using Xunit;
using System.Linq;

namespace ProGlassAutomation.Tests.Agent
{
    // ═══════════════════════════════════════════════════════════════════
    // FILE: Tests/Agent/SplashSurfaceSourceTests.cs
    // FIX-2026-09-05 splash pins — modern redesign surface contract.
    //
    // The splash was fully recomposed (dusk-mesh stage, slim chips,
    // metallic wordmark, hairline progress) while the code-behind
    // choreography stayed byte-for-byte untouched. That split is the
    // defect class these pins guard: the XAML owns LOOK, the .cs owns
    // BEHAVIOUR, and the junction is the 24-name code-behind contract
    // plus the legal/terms surface.
    //
    // Locked facts:
    //   1. All 24 x:Name anchors the code-behind touches exist ONCE.
    //   2. The Baraut location run is gone from the footer (owner
    //      directive); copyright + version runs stay.
    //   3. Frozen palette keys survive (theme-flip regression guard).
    //   4. Terms/login/setup gates + legal copy stay mounted.
    //   5. Window flags (opaque topmost launch) stay pinned.
    // ═══════════════════════════════════════════════════════════════════
    public sealed class SplashSurfaceSourceTests
    {
        [Fact]
        public void CodeBehind_anchors_all_exist_exactly_once()
        {
            string view = View();

            string[] anchors =
            [
                "PosterHost", "LogoBorder", "LogoScale",
                "AppNameText", "AppNameTranslate", "TaglineText",
                "ProgressTrack", "ProgressFill",
                "LoadingText", "LoadingDots", "LoadingStrip",
                "TermsPanel", "TermsBar", "AcceptTermsCheck",
                "DeclineButton", "ContinueButton",
                "LoginHost", "LoginGate", "SetupHost", "SetupGate",
                "TopLeftBadge", "TopCenterPill", "TopRightBadge",
                "FeaturePanel",
            ];

            foreach (string anchor in anchors)
            {
                int hits = Count(view, $"x:Name=\"{anchor}\"");
                Assert.True(hits == 1,
                    $"Code-behind anchor '{anchor}' must exist exactly once, found {hits}.");
            }
        }

        [Fact]
        public void Footer_keeps_copyright_and_version_but_no_location()
        {
            string view = View();

            // Owner directive 2026-09-05: the trailing
            // "· Baraut, Uttar Pradesh, India" run leaves the product.
            Assert.DoesNotContain("Splash.BarautUttarPradeshIndia", view);
            Assert.DoesNotContain("Baraut", view);

            // ...while the legal/version line it padded survives.
            Assert.Contains("Splash.Copyright", view);
            Assert.Contains("Splash.V\"", view);
            Assert.Contains("{x:Static updates:AppInfo.Version}", view);
        }

        [Fact]
        public void Frozen_palette_keys_survive_the_redesign()
        {
            string view = View();

            // The splash is a brand moment: theme brushes were a
            // boot-flip defect, so the palette stays frozen forever.
            foreach (string key in
            [
                "SplashCerulean", "SplashCeruleanDark", "SplashOutline",
                "SplashPillBg", "SplashTrackBg",
                "SplashTextPrimary", "SplashTextSecondary",
                "PageBg", "ProgressGradient", "NameGradient",
            ])
            {
                Assert.Contains($"x:Key=\"{key}\"", view);
            }
        }

        [Fact]
        public void Gates_and_legal_surface_stay_mounted()
        {
            string view = View();

            // UAE legal copy (9 clauses verbatim): rewording bumps
            // TermsVersion in code-behind — the XAML must never invent
            // shorter text.
            for (int i = 1; i <= 9; i++)
                Assert.Contains($"Splash.Term{i}", view);
            Assert.Contains("Splash.AcceptContinue", view);
            Assert.Contains("Splash.DeclineExit", view);
            Assert.Contains("Splash.AcceptText", view);

            // All three mount points the code-behind swaps between.
            Assert.Contains("<login:LoginPanel", view);
            Assert.Contains("<login:FirstRunSetupPanel", view);
            Assert.Contains("Panel.ZIndex=\"10\"", view);   // terms
            Assert.Contains("Panel.ZIndex=\"11\"", view);   // login
            Assert.Contains("Panel.ZIndex=\"12\"", view);   // setup

            // Brand assets.
            Assert.Contains("proglass_icon_256.png", view);
            Assert.Contains("UniformGrid Columns=\"6\"", view);
        }

        [Fact]
        public void Window_flags_stay_the_opaque_topmost_launch()
        {
            string view = View();

            // 2026-08-29 hardening: opaque stage (no AllowsTransparency),
            // Topmost for the fade — the desktop-flash regression class.
            Assert.Contains("WindowState=\"Maximized\"", view);
            Assert.Contains("WindowStyle=\"None\"", view);
            Assert.Contains("Topmost=\"True\"", view);
            Assert.Contains("ShowInTaskbar=\"False\"", view);
            Assert.DoesNotContain("AllowsTransparency", view);
        }

        // ── helpers ──────────────────────────────────────────────────

        private static string View() => File.ReadAllText(Path.Combine(
            LocateProjectRoot(), "Views/Splash/SplashView.xaml"));

        private static int Count(string haystack, string needle)
            => haystack.Split(
                   new[] { needle }, StringSplitOptions.None).Length - 1;

        private static string LocateProjectRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var probe = Path.Combine(dir.FullName,
                    "Views", "ProformaInvoice", "Step5DataGrid", "DataGridView.xaml");
                if (File.Exists(probe)) return dir.FullName;
                dir = dir.Parent;
            }

            throw new DirectoryNotFoundException(
                "Could not locate the project root above " + AppContext.BaseDirectory);
        }
    }
}
