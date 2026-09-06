// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/CrystalFluidGlobalWaterTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// GLOBAL WATER MATERIAL (2026-09-06) — the Ios-17pm animated liquid fill
// was previously confined to three hand-wired hosts (StatCard + the two
// Settings previews). This suite pins the new global integration:
//
//   · an attached "water material" (CrystalWaterAttach.Attach) wraps a
//     card Border's child in a Grid that layers CrystalWaterControl
//     BEHIND the content (glass → water → content),
//   · it self-gates on the global family resource
//     CrystalWaterDefaultEnabled via SetResourceReference (live family
//     switches — no reload), and reads the ambient level from
//     CrystalDefaultWaterLevel,
//   · the shared + per-module card styles opt in with a single Setter,
//     so every module's cards carry water without re-typing the card
//     element.
//
// These are source-pinning tests (the repo's Agent convention): they read
// the shipped files and assert the wiring is present and non-degraded.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.IO;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public sealed class CrystalFluidGlobalWaterTests
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

        private const string Setter =
            "<Setter Property=\"crystal:CrystalWaterAttach.Attach\" Value=\"True\"/>";

        // ── 1. THE ATTACHED MATERIAL EXISTS AND GATES ON THE FAMILY ─────
        [Fact]
        public void Attached_material_gates_on_the_family_resource()
        {
            string src = Read("Controls/CrystalFluid/CrystalWaterAttach.cs");

            Assert.Contains("DependencyProperty.RegisterAttached(",
                src, StringComparison.Ordinal);
            Assert.Contains("\"Attach\",", src, StringComparison.Ordinal);

            // Live family gate + ambient level — DynamicResource semantics
            // (SetResourceReference), so a family switch revalues in place.
            Assert.Contains("CrystalWaterControl.WaterEnabledProperty",
                src, StringComparison.Ordinal);
            Assert.Contains("\"CrystalWaterDefaultEnabled\"",
                src, StringComparison.Ordinal);
            Assert.Contains("CrystalWaterControl.WaterLevelProperty",
                src, StringComparison.Ordinal);
            Assert.Contains("\"CrystalDefaultWaterLevel\"",
                src, StringComparison.Ordinal);

            // Water must never capture input and must follow the card radius.
            Assert.Contains("IsHitTestVisible = false", src,
                StringComparison.Ordinal);
            Assert.Contains("nameof(Border.CornerRadius)", src,
                StringComparison.Ordinal);
        }

        // ── 2. SHARED CARD STYLES OPT IN ────────────────────────────────
        [Fact]
        public void Shared_card_styles_attach_water()
        {
            string cardStyles = Read("Themes/Styles/CardStyles.xaml");
            string dashboard = Read("Styles/DashboardStyles.xaml");
            string design = Read("Themes/Styles/DesignTokens.xaml");
            string dialogs = Read("Themes/Styles/DialogStyles.xaml");

            // BaseCardStyle is the root of Elevated/Stat/Interactive* cards.
            Assert.Contains(Setter, cardStyles, StringComparison.Ordinal);
            // Dashboard tiles + section cards + dialogs.
            Assert.Contains(Setter, dashboard, StringComparison.Ordinal);
            Assert.Contains(Setter, design, StringComparison.Ordinal);
            Assert.Contains(Setter, dialogs, StringComparison.Ordinal);

            // The family-gate resource is still published by the crystal
            // painter, so the attached material actually switches on.
            string engine = Read("Services/Theme/VisualThemeEngine.Part3.cs");
            Assert.Contains("resources[\"CrystalWaterDefaultEnabled\"] = true",
                engine, StringComparison.Ordinal);
        }

        // ── 3. PER-MODULE CARDS OPT IN (NOT JUST THE SHARED SET) ────────
        [Fact]
        public void Per_module_card_styles_attach_water()
        {
            Assert.Contains(Setter, Read(
                "Views/About/AboutStyles.xaml"), StringComparison.Ordinal);
            Assert.Contains(Setter, Read(
                "Views/Settings/SettingsSectionStyles.xaml"), StringComparison.Ordinal);
            Assert.Contains(Setter, Read(
                "Views/ProformaInvoice/Step9ProfitLoss/GlassTypeComparisonView.xaml"),
                StringComparison.Ordinal);
            Assert.Contains(Setter, Read(
                "Views/Reports/StatementGenerator/Styles/WizardStyles.xaml"),
                StringComparison.Ordinal);
            Assert.Contains(Setter, Read(
                "Views/DeliveryNote/DeliveryNoteList/DeliveryNoteListView.xaml"),
                StringComparison.Ordinal);
        }
    }
}
