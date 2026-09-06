// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/CrystalFluidNavAdoptionTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// IOS-17PM P4c — NAV/TOPBAR ADOPTION (2026-09-05).
//
// The shell sidebar/topbar styles (SidebarNavItem, TopIconButton, …)
// are family-neutral and already bind hover/active surfaces through
// DynamicResource role keys that the ENGINE paints per family.
// P4c adopts the authored nav identities at exactly those role keys
// in the crystal paint:
//   MenuHoverBrush  ← CrystalNavHoverBgBrush (authored hover identity)
//   TintIndigoBrush ← CrystalNavActiveBgBrush (authored active fill
//                      / sidebar chip accent)
// Both role keys are ManagedKeys — removed & repainted on every family
// switch — so no authored nav tint can leak into Normal/Monet/…; the
// pre-P4c formulas survive as byte-exact fallbacks when the merged
// authored dictionaries are absent (unit hosts).
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Theme;
using ProGlassAutomation.Tests.TestSupport;
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public sealed class CrystalFluidNavAdoptionTests
    {
        private static string Read(string relative) =>
            File.ReadAllText(Path.Combine(LocateProjectRoot(),
                relative.Replace('/', Path.DirectorySeparatorChar)));

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

        // ── 1. CRYSTAL PAINT ADOPTS AUTHORED NAV ROLE KEYS ──────────────
        [Fact]
        public void Crystal_paint_adopts_authored_nav_identities()
        {
            string part3 = Read("Services/Theme/VisualThemeEngine.Part3.cs");

            Assert.Contains(
                "var hoverFill = TokenBrush(resources,\n                \"CrystalNavHoverBgBrush\")",
                part3, StringComparison.Ordinal);
            Assert.Contains("resources[\"MenuHoverBrush\"] = hoverFill;",
                part3, StringComparison.Ordinal);
            Assert.Contains(
                "resources[\"TintIndigoBrush\"] = TokenBrush(resources,\n                \"CrystalNavActiveBgBrush\")",
                part3, StringComparison.Ordinal);

            // helper exists beside TokenColor; byte-exact fallbacks remain
            Assert.Contains("private static SolidColorBrush TokenBrush(",
                part3, StringComparison.Ordinal);
            Assert.Contains("private static SolidColorBrush FrozenArgb(",
                part3, StringComparison.Ordinal);
            Assert.Contains("dark ? (byte)0x1A : (byte)0x2E,",
                part3, StringComparison.Ordinal);
            Assert.Contains("?? HueFrost(\"TintIndigoBrush\", 0.10, 0.05);",
                part3, StringComparison.Ordinal);
        }

        // ── 2. ROLE KEYS STAY MANAGED — NO CROSS-FAMILY LEAK ────────────
        [Fact]
        public void Nav_role_keys_remain_managed_keys()
        {
            var field = typeof(VisualThemeEngine).GetField(
                "ManagedKeys",
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Static);
            Assert.NotNull(field);
            var keys = (string[])field!.GetValue(null)!;

            Assert.Contains("MenuHoverBrush", keys);
            Assert.Contains("TintIndigoBrush", keys);
        }

        // ── 3. EVERY SURVIVING FAMILY PATH REPAINTS THE ROLE KEYS ───────
        [Fact]
        public void Every_family_painter_owns_menu_hover()
        {
            string p1 = Read("Services/Theme/VisualThemeEngine.Part1.cs");
            string p3 = Read("Services/Theme/VisualThemeEngine.Part3.cs");

            // 2026-09-06 theme reduction: Monet (Part1) and Crystal Fluid
            // (Part3) each write MenuHoverBrush on paint; Normal restores
            // the theme-file default via ClearOverrides. Overwrites (not
            // cleanup) carry each switch.
            Assert.True(Regex.Matches(p1,
                Regex.Escape("resources[\"MenuHoverBrush\"] =")).Count >= 1,
                "Part1 must cover the Monet profile");
            Assert.True(Regex.Matches(p3,
                Regex.Escape("resources[\"MenuHoverBrush\"] =")).Count >= 1,
                "Crystal Fluid paint owns MenuHoverBrush");
        }

        // ── 4. AUTHORED NAV NUMBER HOME EXISTS (colors → brushes) ───────
        [Fact]
        public void Authored_nav_identities_exist_as_tokens()
        {
            string brushes = Read("Themes/CrystalFluid/CrystalFluidBrushes.xaml");
            string colors = Read("Themes/CrystalFluid/CrystalFluidColors.xaml");

            foreach (var pair in new[]
            {
                ("CrystalNavHoverBgBrush",      "CrystalNavHoverBgColor"),
                ("CrystalNavActiveBgBrush",     "CrystalNavActiveBgColor"),
                ("CrystalNavTextBrush",         "CrystalNavTextColor"),
                ("CrystalNavActiveTextBrush",   "CrystalNavActiveTextColor"),
            })
            {
                Assert.Contains($"x:Key=\"{pair.Item1}\"", brushes,
                    StringComparison.Ordinal);
                Assert.Contains($"Color=\"{{StaticResource {pair.Item2}}}\"",
                    brushes, StringComparison.Ordinal);
                Assert.Contains($"x:Key=\"{pair.Item2}\"", colors,
                    StringComparison.Ordinal);
            }
        }

        // ── 5. SHELL NAV STYLE STILL RIDES THE ROLE KEYS ────────────────
        [Fact]
        public void Shell_nav_styles_unchanged_ride_role_keys()
        {
            // P4c must not rewrite ShellStyles.xaml — adoption is purely
            // through role-key paints; guard against accidental drift.
            string shell = Read("Themes/Styles/ShellStyles.xaml");
            Assert.Contains("x:Key=\"SidebarNavItem\"", shell,
                StringComparison.Ordinal);
            int sidenav = shell.IndexOf("x:Key=\"SidebarNavItem\"",
                StringComparison.Ordinal);
            int navEnd = shell.IndexOf("x:Key=\"SidebarSubItem\"", sidenav,
                StringComparison.Ordinal);
            Assert.True(sidenav > 0 && navEnd > sidenav);
            string block = shell.Substring(sidenav, navEnd - sidenav);
            Assert.Contains("{DynamicResource MenuHoverBrush}", block,
                StringComparison.Ordinal);
            Assert.Contains("{DynamicResource TintIndigoBrush}", block,
                StringComparison.Ordinal);

            // crystal nav reference styles remain intact for file-20
            // compositions (used by the P4e preview page)
            string nav = Read("Themes/CrystalFluid/CrystalFluidNavigation.xaml");
            Assert.Contains("x:Key=\"CrystalNavButtonStyle\"", nav,
                StringComparison.Ordinal);
            Assert.Contains("x:Key=\"CrystalNavLabelStyle\"", nav,
                StringComparison.Ordinal);
        }
    }
}
