// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Performance/PerformanceProfileService.cs
// PROJECT: ProGlassAutomation
// MODULE: Dynamic Theme & Motion Engine (hardware adaptation)
//
// Detects the display refresh rate and snaps it to the nearest tier in
// 60 / 90 / 120 / 144 / 165 fps. The whole UI then adapts:
//
//   • MotionSpeedRatio  — scales animation durations. High-refresh
//                          displays get snappier (shorter) motion so
//                          movement reads as smooth instead of slow.
//   • RichEffects       — blur/glow/shadow depth is unlocked only when
//                          the hardware can redraw it cheaply (>=120).
//   • DesiredFrameRate  — WPF storyboards get an explicit frame-rate
//                          hint matching the panel, so 144 Hz monitors
//                          actually animate at 144 fps instead of the
//                          default 60.
//
// The service never throws — if detection fails the app simply runs at
// the safe 60 fps profile.
// ═══════════════════════════════════════════════════════════════════════

using System;
using ProGlassAutomation.Services.Logging;
using System.Runtime.InteropServices;
using System.Windows;

namespace ProGlassAutomation.Services.Performance
{
    /// <summary>Hardware refresh tier the UI adapts to.</summary>
    public enum FpsTier
    {
        Fps60 = 60,
        Fps90 = 90,
        Fps120 = 120,
        Fps144 = 144,
        Fps165 = 165
    }

    /// <summary>
    /// Detects display capability and publishes motion/quality settings
    /// as application resources the XAML binds to via DynamicResource:
    ///
    ///   MotionSpeedRatio   (double)   — duration multiplier for animations
    ///   MotionDurationFast (Duration) — 120ms scaled
    ///   MotionDurationNorm (Duration) — 220ms scaled
    ///   MotionDurationSlow (Duration) — 380ms scaled
    ///   RichEffectsEnabled (bool)     — heavy blur/glow allowed
    ///   TargetFps          (int)      — detected tier
    /// </summary>
    public static class PerformanceProfileService
    {
        /// <summary>Active tier (default: safe 60 fps profile).</summary>
        public static FpsTier Tier { get; private set; } = FpsTier.Fps60;

        /// <summary>Numeric fps of the active tier.</summary>
        public static int TargetFps => (int)Tier;

        /// <summary>
        /// Multiplier applied to animation durations.
        /// Higher refresh → smaller ratio → snappier, smoother motion.
        /// </summary>
        public static double MotionSpeedRatio => Tier switch
        {
            FpsTier.Fps90  => 0.88,
            FpsTier.Fps120 => 0.78,
            FpsTier.Fps144 => 0.70,
            FpsTier.Fps165 => 0.64,
            _              => 1.00
        };

        /// <summary>
        /// True when the hardware can afford expensive effects
        /// (drop shadows with large blur radii, glow borders, etc.).
        /// </summary>
        public static bool RichEffectsEnabled => TargetFps >= 120;

        /// <summary>True on ultra-high-refresh panels (144+).</summary>
        public static bool UltraSmooth => TargetFps >= 144;

        /// <summary>Raised after Apply() publishes new resources.</summary>
        public static event EventHandler? ProfileApplied;

        // ── Win32 display detection ───────────────────────────────────

        private const int ENUM_CURRENT_SETTINGS = -1;
        private const int CDS_TEST = 0x00000002;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        private struct DEVMODE
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmDeviceName;
            public short dmSpecVersion;
            public short dmDriverVersion;
            public short dmSize;
            public short dmDriverExtra;
            public int dmFields;
            public int dmPositionX;
            public int dmPositionY;
            public int dmDisplayOrientation;
            public int dmDisplayFixedOutput;
            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmFormName;
            public short dmLogPixels;
            public int dmBitsPerPel;
            public int dmPelsWidth;
            public int dmPelsHeight;
            public int dmDisplayFlags;
            public int dmDisplayFrequency;   // ← refresh rate in Hz
            public int dmICMMethod;
            public int dmICMIntent;
            public int dmMediaType;
            public int dmDitherType;
            public int dmReserved1;
            public int dmReserved2;
            public int dmPanningWidth;
            public int dmPanningHeight;
        }

        [DllImport("user32.dll", CharSet = CharSet.Ansi)]
        private static extern bool EnumDisplaySettings(
            string? deviceName, int modeNum, ref DEVMODE devMode);

        // ══════════════════════════════════════════════════════════════
        //  DETECT + APPLY
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Reads the current display refresh rate and snaps it to the
        /// nearest supported tier. Returns the detected raw Hz (0 when
        /// detection failed).
        /// </summary>
        public static int DetectRefreshRate()
        {
            try
            {
                var devMode = new DEVMODE
                {
                    dmSize = (short)Marshal.SizeOf<DEVMODE>()
                };

                if (!EnumDisplaySettings(null, ENUM_CURRENT_SETTINGS, ref devMode))
                    return 0;

                return devMode.dmDisplayFrequency;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// Detects the panel tier and publishes all motion resources.
        /// Call once from App startup; safe to call again after the
        /// user changes display mode or overrides the tier in Settings.
        /// Never throws.
        /// </summary>
        public static void Apply(FpsTier? manualOverride = null)
        {
            try
            {
                if (manualOverride.HasValue)
                {
                    Tier = manualOverride.Value;
                }
                else
                {
                    int hz = DetectRefreshRate();
                    Tier = SnapToTier(hz);
                }

                PublishResources();
                ProfileApplied?.Invoke(null, EventArgs.Empty);

                AppLogService.Debug(
                    $"[Performance] Tier: {TargetFps} fps " +
                    $"(speed ratio {MotionSpeedRatio:0.00}, " +
                    $"rich effects {RichEffectsEnabled})");
            }
            catch (Exception ex)
            {
                AppLogService.Debug(
                    $"[Performance] Apply failed: {ex.Message}");
            }
        }

        /// <summary>Snaps a raw Hz reading to the nearest tier.</summary>
        public static FpsTier SnapToTier(int hz)
        {
            if (hz <= 0) return FpsTier.Fps60;

            // Pick the tier whose distance to the reading is smallest.
            FpsTier best = FpsTier.Fps60;
            int bestDistance = int.MaxValue;

            foreach (FpsTier tier in Enum.GetValues<FpsTier>())
            {
                int distance = Math.Abs((int)tier - hz);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = tier;
                }
            }

            // Don't claim a high tier for wildly off readings
            // (e.g. remote desktop reporting 0/30 Hz).
            if (bestDistance > 12) return FpsTier.Fps60;
            return best;
        }

        // ══════════════════════════════════════════════════════════════
        //  RESOURCE PUBLISHING
        // ══════════════════════════════════════════════════════════════

        private static void PublishResources()
        {
            var resources = Application.Current?.Resources;
            if (resources == null) return;

            double ratio = MotionSpeedRatio;

            resources["TargetFps"] = TargetFps;
            resources["MotionSpeedRatio"] = ratio;
            resources["RichEffectsEnabled"] = RichEffectsEnabled;
            resources["UltraSmoothEnabled"] = UltraSmooth;

            resources["MotionDurationFast"] =
                new Duration(TimeSpan.FromMilliseconds(120 * ratio));
            resources["MotionDurationNorm"] =
                new Duration(TimeSpan.FromMilliseconds(220 * ratio));
            resources["MotionDurationSlow"] =
                new Duration(TimeSpan.FromMilliseconds(380 * ratio));

            // Theme Studio: the Animation Intensity slider scales the
            // same tokens, so re-snapshot and re-apply the studio layer
            // on top after a refresh-rate re-profile (startup runs
            // PublishResources BEFORE the studio restore, so startup
            // order stays intact; OnFamilyApplied is a no-op when the
            // studio layer is inactive).
            Theme.ThemeStudioService.OnFamilyApplied();

            // NOTE: an earlier revision globally overrode
            // Timeline.DesiredFrameRateProperty metadata here. That
            // touched WPF's internal animation plumbing for EVERY
            // timeline (including framework popups/dropdowns) and was
            // rolled back — stability beats a speculative micro-gain.
            // High-refresh smoothness now comes from the scaled
            // MotionDuration tokens above, which XAML consumes safely.
        }
    }
}
