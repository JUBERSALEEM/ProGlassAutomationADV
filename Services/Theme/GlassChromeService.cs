// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Theme/GlassChromeService.cs
// PROJECT: ProGlassAutomation
// MODULE: Window-level acrylic blur chrome (iOS-style glass)
//
// Real "iOS glass" is a backdrop blur + white tint + translucency.
// WPF has no per-surface backdrop blur, but the Desktop Window Manager
// does: SetWindowCompositionAttribute can give the whole window the
// ACCENT_ENABLE_ACRYLICBLURBEHIND treatment (Windows 10 1809+ /
// Windows 11), or the older ACCENT_ENABLE_BLURBEHIND (Windows 10
// 1507-1803).  The visual theme engine then paints the window surface
// with a translucent veil, so the blurred desktop + accent tint reads
// through EVERY transparent card — the frosted material look.
//
// Behaviour contract:
//   * Attach(Window)   — called once by MainWindow after the HWND exists.
//   * Apply(tint)      — turns the accent on with an accent-derived tint
//                        (0xAABBGGRR packing, like DWM expects).
//   * Remove()         — restores the normal window surface.
//   * IsSupported      — false on Windows < 10 or when no window is
//                        attached; the theme engine then falls back to
//                        its opaque gradient stage so the UI never
//                        becomes invisible.
//   * Everything degrades gracefully: DWM silently disables the blur on
//     remote sessions / when "Transparency effects" is off, rendering
//     the supplied tint as a solid backdrop instead.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Logging;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace ProGlassAutomation.Services.Theme
{
    /// <summary>
    /// Applies/removes the DWM accent policy (acrylic blur) on the main
    /// window so glass visual families get a real frosted backdrop.
    /// </summary>
    public static class GlassChromeService
    {
        // ── DWM accent policy interop ─────────────────────────────────

        private enum AccentState
        {
            Disabled = 0,
            EnableGradient = 1,
            EnableTransparentGradient = 2,
            EnableBlurBehind = 3,
            EnableAcrylicBlurBehind = 4,
            EnableHostBackdrop = 5
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct AccentPolicy
        {
            public int AccentState;
            public int AccentFlags;
            public int GradientColor; // 0xAABBGGRR (DWM's own packing)
            public int AnimationId;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WindowCompositionAttributeData
        {
            public int Attribute;      // WCA_ACCENT_POLICY = 19
            public IntPtr Data;        // pinned AccentPolicy
            public int SizeOfData;
        }

        private const int WcaAccentPolicy = 19;

        [DllImport("user32.dll")]
        private static extern int SetWindowCompositionAttribute(
            IntPtr hwnd, ref WindowCompositionAttributeData data);

        // ── State ─────────────────────────────────────────────────────

        private static Window? _window;
        private static IntPtr _hwnd = IntPtr.Zero;
        private static bool _attached;
        private static bool _capabilityProbed;

        /// <summary>True when the OS can back the window with blur.</summary>
        public static bool IsSupported { get; private set; }

        /// <summary>True while the accent policy is currently applied.</summary>
        public static bool IsActive { get; private set; }

        /// <summary>
        /// Binds the service to the main window. Must be called after the
        /// window's HWND exists (any time after InitializeComponent).
        /// Never throws.
        /// </summary>
        public static void Attach(Window window)
        {
            try
            {
                if (window == null) return;
                _window = window;

                var helper = new WindowInteropHelper(window);
                helper.EnsureHandle();
                _hwnd = helper.Handle;
                _attached = _hwnd != IntPtr.Zero;

                if (!_capabilityProbed)
                {
                    IsSupported = OsSupportsBackdrop();
                    _capabilityProbed = true;
                }
            }
            catch (Exception ex)
            {
                _attached = false;
                IsSupported = false;
                AppLogService.Warning(
                    $"Glass chrome attach failed: {ex.Message}",
                    new LogContext
                    {
                        Module = "Themes",
                        Class = nameof(GlassChromeService)
                    });
            }
        }

        /// <summary>
        /// Applies the acrylic (or legacy blur) accent policy with the
        /// given tint. No-op when unsupported. Never throws.
        /// </summary>
        public static void Apply(Color tint)
        {
            try
            {
                if (!_attached || !IsSupported) return;

                // Windows 10 1809+ / Windows 11: true acrylic blur.
                // Windows 10 1507-1803: legacy blur-behind.
                int state = Environment.OSVersion.Version.Build >= 17763
                    ? (int)AccentState.EnableAcrylicBlurBehind
                    : (int)AccentState.EnableBlurBehind;

                var policy = new AccentPolicy
                {
                    AccentState = state,
                    AccentFlags = 0,
                    // DWM expects 0xAABBGGRR — alpha first, channels reversed.
                    GradientColor =
                        (tint.A << 24) | (tint.B << 16) | (tint.G << 8) | tint.R,
                    AnimationId = 0
                };

                int policySize = Marshal.SizeOf<AccentPolicy>();
                IntPtr ptr = Marshal.AllocHGlobal(policySize);
                try
                {
                    Marshal.StructureToPtr(policy, ptr, false);

                    var data = new WindowCompositionAttributeData
                    {
                        Attribute = WcaAccentPolicy,
                        Data = ptr,
                        SizeOfData = policySize
                    };

                    SetWindowCompositionAttribute(_hwnd, ref data);
                    IsActive = true;
                }
                finally
                {
                    Marshal.FreeHGlobal(ptr);
                }

                AppLogService.Info(
                    "Window glass chrome enabled (acrylic/blur backdrop)",
                    new LogContext
                    {
                        Module = "Themes",
                        Class = nameof(GlassChromeService),
                        Operation = "Apply"
                    });
            }
            catch (Exception ex)
            {
                AppLogService.Warning(
                    $"Window glass chrome apply failed: {ex.Message}",
                    new LogContext
                    {
                        Module = "Themes",
                        Class = nameof(GlassChromeService)
                    });
            }
        }

        /// <summary>Restores the normal window surface. Never throws.</summary>
        public static void Remove()
        {
            try
            {
                if (!_attached || !IsActive) return;

                var policy = new AccentPolicy
                {
                    AccentState = (int)AccentState.Disabled,
                    AccentFlags = 0,
                    GradientColor = 0,
                    AnimationId = 0
                };

                int policySize = Marshal.SizeOf<AccentPolicy>();
                IntPtr ptr = Marshal.AllocHGlobal(policySize);
                try
                {
                    Marshal.StructureToPtr(policy, ptr, false);

                    var data = new WindowCompositionAttributeData
                    {
                        Attribute = WcaAccentPolicy,
                        Data = ptr,
                        SizeOfData = policySize
                    };

                    SetWindowCompositionAttribute(_hwnd, ref data);
                }
                finally
                {
                    Marshal.FreeHGlobal(ptr);
                }

                IsActive = false;
            }
            catch (Exception ex)
            {
                AppLogService.Warning(
                    $"Window glass chrome removal failed: {ex.Message}",
                    new LogContext
                    {
                        Module = "Themes",
                        Class = nameof(GlassChromeService)
                    });
            }
        }

        // ── Capability probe ──────────────────────────────────────────

        /// <summary>
        /// Windows 10+ supports the accent policy (blur-behind from
        /// build 10240, acrylic from build 17763). Older Windows gets the
        /// opaque fallback stage.
        /// </summary>
        private static bool OsSupportsBackdrop()
        {
            try
            {
                var os = Environment.OSVersion;
                if (os.Platform != PlatformID.Win32NT) return false;
                // Windows 10 = 6.2/10.0 with build >= 10240.
                return os.Version.Major >= 10 && os.Version.Build >= 10240;
            }
            catch
            {
                return false;
            }
        }
    }
}
