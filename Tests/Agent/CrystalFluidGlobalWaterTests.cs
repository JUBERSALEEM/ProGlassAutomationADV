// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/CrystalFluidGlobalWaterTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// GLOBAL WATER MATERIAL v2 (2026-09-06) — the Ios-17pm animated liquid
// fill is delivered to EVERY glass card through ONE app-wide class
// handler, not per-style setters. This suite pins the wiring:
//
//   · a class handler on Border.Loaded (CrystalWaterGlobal.Install,
//     registered once at startup) routes every Border through
//     CrystalWaterAttach.TryAutoAttach,
//   · TryAutoAttach recognises a "card" deterministically (Background
//     reference-equals one of the engine's glass SURFACE brushes) and
//     wraps the Border's child in glass → water → content,
//   · the water self-gates on CrystalWaterDefaultEnabled +
//     CrystalDefaultWaterLevel via SetResourceReference (DynamicResource
//     semantics → live family switch, no reload),
//   · the material never fires under non-crystal families (the gate
//     resource is published True only by the crystal painter).
//
// These are source-pinning tests (the repo's Agent convention).
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

        // ── 1. ONE GLOBAL DELIVERY POINT ────────────────────────────────
        [Fact]
        public void Water_is_delivered_through_one_class_handler()
        {
            string global = Read("Controls/CrystalFluid/CrystalWaterGlobal.cs");

            Assert.Contains("EventManager.RegisterClassHandler(",
                global, StringComparison.Ordinal);
            Assert.Contains("typeof(Border)", global, StringComparison.Ordinal);
            Assert.Contains("FrameworkElement.LoadedEvent",
                global, StringComparison.Ordinal);
            Assert.Contains("CrystalWaterAttach.TryAutoAttach(border)",
                global, StringComparison.Ordinal);

            // Installed exactly once at startup, beside the existing
            // ViewportFitBehavior class-handler registration.
            string startup = Read("App.StartupCore.cs");
            Assert.Contains("CrystalWaterGlobal.Install();",
                startup, StringComparison.Ordinal);
        }

        // ── 2. DETERMINISTIC CARD RECOGNITION ───────────────────────────
        [Fact]
        public void Cards_are_recognised_by_glass_surface_brush_reference()
        {
            string src = Read("Controls/CrystalFluid/CrystalWaterAttach.cs");

            Assert.Contains("TryAutoAttach", src, StringComparison.Ordinal);
            Assert.Contains("IsCardSurface", src, StringComparison.Ordinal);
            Assert.Contains("ReferenceEquals(brush, background)",
                src, StringComparison.Ordinal);

            // The engine's glass SURFACE brushes qualify a Border as a
            // card; hover/tint/accent fills never do.
            Assert.Contains("\"GlassUltraClearBrush\"", src, StringComparison.Ordinal);
            Assert.Contains("\"ModuleBackgroundBrush\"", src, StringComparison.Ordinal);
            Assert.Contains("\"ElevatedSurfaceBrush\"", src, StringComparison.Ordinal);

            // Rounded corners separate cards from rectangular glass chrome
            // (tab strips, rails, footer bars — radius 0).
            Assert.Contains("border.CornerRadius", src, StringComparison.Ordinal);

            // Guards: no child / already-hosting-water / card ancestor.
            Assert.Contains("ContainsWaterControl", src, StringComparison.Ordinal);
            Assert.Contains("HasCardAncestor", src, StringComparison.Ordinal);
        }

        // ── 2b. REPARENT SAFELY (regression: startup crash) ─────────────
        [Fact]
        public void Wrap_disconnects_the_original_child_before_reparenting()
        {
            // ERR-3F0957: EnsureWrapped added the still-connected original
            // child into the wrapper Grid, and WPF threw "Specified element
            // is already the logical child of another element" — the crash
            // left the app stuck on the splash screen. The original MUST be
            // detached from the Border before it is re-added to the wrapper.
            string src = Read("Controls/CrystalFluid/CrystalWaterAttach.cs");

            int disconnect = src.IndexOf("border.Child = null",
                StringComparison.Ordinal);
            int reAdd = src.IndexOf("wrapper.Children.Add(original)",
                StringComparison.Ordinal);

            Assert.True(disconnect >= 0,
                "EnsureWrapped must detach Border.Child before reparenting.");
            Assert.True(reAdd > disconnect,
                "The original child must be disconnected BEFORE being added to the wrapper.");
        }

        // ── 3. LIVE FAMILY GATE (no reload) ─────────────────────────────
        [Fact]
        public void Water_gates_on_the_family_resource_and_ambient_level()
        {
            string src = Read("Controls/CrystalFluid/CrystalWaterAttach.cs");

            // SetResourceReference = DynamicResource semantics → a runtime
            // family switch revalues the attached water in place.
            Assert.Contains("CrystalWaterControl.WaterEnabledProperty",
                src, StringComparison.Ordinal);
            Assert.Contains("\"CrystalWaterDefaultEnabled\"",
                src, StringComparison.Ordinal);
            Assert.Contains("CrystalWaterControl.WaterLevelProperty",
                src, StringComparison.Ordinal);
            Assert.Contains("\"CrystalDefaultWaterLevel\"",
                src, StringComparison.Ordinal);

            // Water never captures input and follows the card radius.
            Assert.Contains("IsHitTestVisible = false", src,
                StringComparison.Ordinal);
            Assert.Contains("nameof(Border.CornerRadius)", src,
                StringComparison.Ordinal);

            // The crystal painter is still the only publisher of True.
            string engine = Read("Services/Theme/VisualThemeEngine.Part3.cs");
            Assert.Contains("resources[\"CrystalWaterDefaultEnabled\"] = true",
                engine, StringComparison.Ordinal);
        }
    }
}
