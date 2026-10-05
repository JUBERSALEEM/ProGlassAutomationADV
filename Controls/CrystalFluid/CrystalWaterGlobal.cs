// ═══════════════════════════════════════════════════════════════════════
// FILE: Controls/CrystalFluid/CrystalWaterGlobal.cs
// PROJECT: ProGlassAutomation
//
// GLOBAL WATER MATERIAL — delivery (2026-09-06). A single class-level
// handler on Border.Loaded means EVERY Border in the app (styled cards,
// inline <Border.Style> cards, future views) is offered the water
// material through CrystalWaterAttach.TryAutoAttach — no per-style
// setters, no per-file edits. Registration mirrors the repo's existing
// ViewportFitBehavior.Install() class-handler pattern and is invoked
// once at startup (App.StartupCoreAsync).
// ═══════════════════════════════════════════════════════════════════════

using System.Windows;
using System.Windows.Controls;

namespace ProGlassAutomation.Controls.CrystalFluid
{
    /// <summary>Registers the app-wide Border.Loaded class handler that
    /// auto-attaches the water material to every glass card.</summary>
    public static class CrystalWaterGlobal
    {
        private static bool _installed;

        /// <summary>Live water auto-attach retired. Cards no longer wrap
        /// CrystalWaterControl. Method kept so startup still compiles.</summary>
        public static void Install()
        {
            _installed = true;
        }

        private static void OnBorderLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is Border border)
                CrystalWaterAttach.TryAutoAttach(border);
        }
    }
}
