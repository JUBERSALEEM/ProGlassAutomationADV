// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Theme/SystemBackdropService.cs
// PROJECT: ProGlassAutomation
// MODULE: OS-level window backdrops (2026-09-05) — the honest answer to
//         "WPF does not support blur".
//
// WHY THIS SERVICE (senior analysis, no marketing):
//   WPF renders through a retained visual tree — without composition
//   interop there is NO per-card GPU backdrop blur; third-party toolkit
//   packages (and composition swap-chains) buy the effect with heavy
//   dependencies and DPI/airspace pitfalls. What Windows DOES give us
//   natively, with zero packages, is the DWM system backdrop that
//   Windows 11 itself uses for Mica/Acrylic apps:
//
//     DwmSetWindowAttribute(hwnd, DWMWA_SYSTEMBACKDROP_TYPE=38, type)
//       1 None · 2 Mica · 3 Acrylic(transient) · 4 Mica-Alt/tabbed
//
//   The whole window backdrop is then rendered BY WINDOWS, outside
//   WPF's renderer — that is literally "bypassing the WPF limitation
//   with the platform service". Where WPF content pixels are kept
//   translucent (our stage brush opacity is lowered while the mode is
//   on) the OS backdrop shows through, and WPF stays in full control
//   of every card, text and animation above it.
//
//   Status: EXPERIMENTAL — needs Windows 11 22H2 (build 22621+). On any
//   older OS or any failure it degrades silently to the painted liquid
//   glass stage (the primary system stays the default everywhere).
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace ProGlassAutomation.Services.Theme
{
    /// <summary>Windows 11 DWM system backdrops (Mica / Acrylic) applied
    /// to the main window — OS-rendered blur behind WPF content.</summary>
    public static class SystemBackdropService
    {
        public const string Off = "Off";
        public const string Mica = "Mica";
        public const string Acrylic = "Acrylic";

        private const int DWMWA_SYSTEMBACKDROP_TYPE = 38;
        private const int DWMSBT_NONE = 1;
        private const int DWMSBT_MAINWINDOW = 2;      // Mica
        private const int DWMSBT_TRANSIENTWINDOW = 3; // Acrylic
        private const int DWMSBT_TABBEDWINDOW = 4;    // Mica Alt

        [DllImport("dwmapi.dll", PreserveSig = true)]
        private static extern int DwmSetWindowAttribute(
            IntPtr hwnd, int attribute, ref int value, int valueSize);

        /// <summary>Windows 11 22H2+ exposes the system-backdrop
        /// attribute — that is the whole support matrix.</summary>
        public static bool IsSupported =>
            Environment.OSVersion.Platform == PlatformID.Win32NT
            && Environment.OSVersion.Version.Build >= 22621;

        /// <summary>
        /// Applies (or clears) the OS backdrop on a window. Never throws;
        /// returns true only when the attribute call actually succeeded.
        /// </summary>
        public static bool Apply(Window? window, string mode)
        {
            try
            {
                if (window == null || !IsSupported) return false;
                var helper = new WindowInteropHelper(window);
                IntPtr hwnd = helper.EnsureHandle();
                if (hwnd == IntPtr.Zero) return false;

                int type = mode == Mica
                    ? DWMSBT_MAINWINDOW
                    : mode == Acrylic
                        ? DWMSBT_TRANSIENTWINDOW
                        : DWMSBT_NONE;
                int hr = DwmSetWindowAttribute(
                    hwnd, DWMWA_SYSTEMBACKDROP_TYPE, ref type, sizeof(int));
                return hr == 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Replaces the window-stage brush with a translucent
        /// copy so the OS backdrop can shine through (only while a Mica/
        /// Acrylic mode is ON). Returning to Off repaints the full stage
        /// via the normal theme chain, so nothing sticks.</summary>
        public static void GhostWindowStage(ResourceDictionary resources)
        {
            if (resources["WindowBackgroundBrush"] is Brush current)
            {
                // 2026-09-09: 0.22 read as "broken see-through ugly" —
                // the OS texture flooded every surface and body text
                // sank into it. 0.45 keeps the Mica/Acrylic material
                // visible while the app's own stage stays readable.
                var ghost = current.Clone();
                ghost.Opacity = 0.45;
                resources["WindowBackgroundBrush"] = ghost;
                ThemeStudioService.UpdateBase("WindowBackgroundBrush", ghost);
            }
        }
    }
}
