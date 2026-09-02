// ═══════════════════════════════════════════════════════════════════════
//  FILE: Agent/Demo/Recording/GdiWindowCapture.cs
//  PROJECT: ProGlassAutomation
//  CREATED: 2026-09-02 — real screen recording, slice B
//
//  OFF-UI-THREAD WINDOW CAPTURE
//  ---------------------------------------------------------------------
//  The old recorder captured with RenderTargetBitmap.Render(window). That
//  is not a screen grab: it re-rasterises the whole WPF visual tree IN
//  SOFTWARE, ON THE UI THREAD, once per frame. It is the reason the app
//  stuttered while recording, and it structurally cannot see popups,
//  dropdowns, ComboBox lists, context menus or dialogs, because those are
//  separate top-level HWNDs that are not children of MainWindow's visual
//  tree.
//
//  This captures the same way OBS's "Window Capture" does on Windows 10+:
//
//    PrintWindow(hwnd, hdc, PW_RENDERFULLCONTENT)
//
//  PW_RENDERFULLCONTENT (0x2, Windows 8.1+) asks DWM for the window's
//  ALREADY-COMPOSITED surface — the exact pixels on screen, including
//  hardware-accelerated and layered content. Crucially it runs entirely
//  on the CALLING thread against the OS, so it can be driven from a
//  background thread and never touches the WPF dispatcher.
//
//  WHY GDI AND NOT Windows.Graphics.Capture
//  WGC is the newer API, but it needs CsWinRT projections, a D3D11 device,
//  a WinRT dispatcher pump and a Windows 10 1903+ contract. That is a
//  large unverifiable surface for an environment with no compiler. GDI
//  PrintWindow needs nothing but user32/gdi32, which this codebase already
//  P/Invokes, and it solves both headline problems: off-thread, and real
//  composited output.
//
//  APP-ONLY BY DESIGN
//  The capture is bound to a single HWND, so the desktop, other apps and
//  notifications never appear — which is the stated requirement. Owned
//  popups belonging to the app ARE included by DWM because they composite
//  into the same surface.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Runtime.InteropServices;

namespace ProGlassAutomation.Agent.Demo.Recording
{
    /// <summary>
    /// One captured frame: raw 32-bit BGRA pixels, top-down.
    /// </summary>
    public sealed class CapturedBitmap
    {
        public required byte[] Pixels { get; init; }
        public int Width { get; init; }
        public int Height { get; init; }
        public int Stride => Width * 4;
    }

    /// <summary>
    /// Reusable GDI capture surface for one window. Allocate once, call
    /// <see cref="TryCapture"/> per frame.
    ///
    /// NOT thread-safe: create one per capture loop and keep it on that
    /// loop's thread. Reusing the DC and DIB across frames is what keeps
    /// per-frame cost down — allocating a 4K bitmap 30 times a second
    /// would thrash the GDI heap.
    /// </summary>
    public sealed class GdiWindowCapture : IDisposable
    {
        private const uint PW_RENDERFULLCONTENT = 0x00000002;
        private const int SRCCOPY = 0x00CC0020;
        private const int BI_RGB = 0;
        private const int DIB_RGB_COLORS = 0;

        private IntPtr _hwnd;
        private IntPtr _memDc;
        private IntPtr _dib;
        private IntPtr _oldObj;
        private IntPtr _bits;
        private int _width;
        private int _height;
        private bool _disposed;

        public int Width => _width;
        public int Height => _height;

        /// <summary>
        /// True when the last capture attempt produced an all-black frame.
        /// Some drivers refuse PW_RENDERFULLCONTENT; the recorder uses this
        /// to fall back rather than silently writing a black video.
        /// </summary>
        public bool LastFrameWasBlank { get; private set; }

        /// <summary>
        /// Prepares (or re-prepares) the capture surface for a window.
        /// Safe to call every frame — it only reallocates when the window
        /// size actually changed, which is what makes resizing mid-record
        /// work instead of tearing.
        /// </summary>
        public bool Prepare(IntPtr hwnd)
        {
            if (_disposed) return false;
            if (hwnd == IntPtr.Zero) return false;
            if (!IsWindow(hwnd)) return false;
            if (IsIconic(hwnd)) return false;   // minimised: nothing to grab

            if (!GetClientRect(hwnd, out RECT rc)) return false;

            int w = rc.Right - rc.Left;
            int h = rc.Bottom - rc.Top;
            if (w < 2 || h < 2) return false;

            // Even dimensions: yuv420p (what every H.264 encoder wants)
            // cannot represent odd width/height.
            w &= ~1;
            h &= ~1;

            if (hwnd == _hwnd && w == _width && h == _height && _dib != IntPtr.Zero)
                return true;    // reuse

            ReleaseSurface();

            IntPtr screenDc = GetDC(IntPtr.Zero);
            if (screenDc == IntPtr.Zero) return false;

            try
            {
                _memDc = CreateCompatibleDC(screenDc);
                if (_memDc == IntPtr.Zero) return false;

                var bmi = new BITMAPINFO
                {
                    bmiHeader = new BITMAPINFOHEADER
                    {
                        biSize = Marshal.SizeOf<BITMAPINFOHEADER>(),
                        biWidth = w,
                        // NEGATIVE height = top-down DIB. Without this the
                        // rows come back bottom-up and every frame is
                        // vertically mirrored.
                        biHeight = -h,
                        biPlanes = 1,
                        biBitCount = 32,
                        biCompression = BI_RGB
                    }
                };

                _dib = CreateDIBSection(
                    _memDc, ref bmi, DIB_RGB_COLORS, out _bits, IntPtr.Zero, 0);

                if (_dib == IntPtr.Zero || _bits == IntPtr.Zero)
                {
                    ReleaseSurface();
                    return false;
                }

                _oldObj = SelectObject(_memDc, _dib);
                _hwnd = hwnd;
                _width = w;
                _height = h;
                return true;
            }
            finally
            {
                ReleaseDC(IntPtr.Zero, screenDc);
            }
        }

        /// <summary>
        /// Captures one frame into <paramref name="buffer"/>.
        ///
        /// The caller owns the buffer so the hot path allocates nothing:
        /// at 4K/60 a fresh 33 MB array per frame would bury the GC.
        /// Returns false when the window is gone, minimised or unreadable.
        /// </summary>
        public bool TryCapture(byte[] buffer)
        {
            if (_disposed || _dib == IntPtr.Zero || _bits == IntPtr.Zero)
                return false;

            int needed = _width * 4 * _height;
            if (buffer == null || buffer.Length < needed) return false;

            // PW_RENDERFULLCONTENT asks DWM for the composited surface.
            // Older/odd drivers ignore the flag and return black, so the
            // result is checked below rather than trusted.
            bool ok = PrintWindow(_hwnd, _memDc, PW_RENDERFULLCONTENT);

            if (!ok)
            {
                // Fallback: blit straight from the window DC. Loses
                // layered/DirectComposition content but is better than
                // dropping the frame entirely.
                IntPtr src = GetDC(_hwnd);
                if (src == IntPtr.Zero) return false;
                try
                {
                    ok = BitBlt(_memDc, 0, 0, _width, _height, src, 0, 0, SRCCOPY);
                }
                finally
                {
                    ReleaseDC(_hwnd, src);
                }
                if (!ok) return false;
            }

            Marshal.Copy(_bits, buffer, 0, needed);

            LastFrameWasBlank = IsMostlyBlank(buffer, needed);
            return true;
        }

        /// <summary>
        /// Cheap sparse scan for an all-black frame. Full scans of a 4K
        /// buffer every frame would themselves cost milliseconds, so this
        /// samples a stride across the buffer — enough to catch the
        /// "driver returned nothing" case without measurable cost.
        /// </summary>
        private static bool IsMostlyBlank(byte[] buf, int len)
        {
            int step = Math.Max(4, (len / 4096) & ~3);
            for (int i = 0; i + 2 < len; i += step)
            {
                if (buf[i] > 8 || buf[i + 1] > 8 || buf[i + 2] > 8)
                    return false;
            }
            return true;
        }

        private void ReleaseSurface()
        {
            if (_memDc != IntPtr.Zero && _oldObj != IntPtr.Zero)
            {
                try { SelectObject(_memDc, _oldObj); } catch { /* teardown */ }
                _oldObj = IntPtr.Zero;
            }
            if (_dib != IntPtr.Zero)
            {
                try { DeleteObject(_dib); } catch { /* teardown */ }
                _dib = IntPtr.Zero;
            }
            if (_memDc != IntPtr.Zero)
            {
                try { DeleteDC(_memDc); } catch { /* teardown */ }
                _memDc = IntPtr.Zero;
            }
            _bits = IntPtr.Zero;
            _hwnd = IntPtr.Zero;
            _width = 0;
            _height = 0;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            ReleaseSurface();
        }

        // ── P/Invoke ─────────────────────────────────────────────────────

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFOHEADER
        {
            public int biSize;
            public int biWidth;
            public int biHeight;
            public short biPlanes;
            public short biBitCount;
            public int biCompression;
            public int biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public int biClrUsed;
            public int biClrImportant;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFO
        {
            public BITMAPINFOHEADER bmiHeader;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
            public int[] bmiColors;
        }

        [DllImport("user32.dll")]
        private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);

        [DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr hwnd, out RECT rect);

        [DllImport("user32.dll")]
        private static extern bool IsWindow(IntPtr hwnd);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hwnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hwnd);

        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateDIBSection(
            IntPtr hdc, ref BITMAPINFO bmi, int usage,
            out IntPtr bits, IntPtr section, int offset);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr obj);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern bool BitBlt(
            IntPtr dest, int x, int y, int w, int h,
            IntPtr src, int sx, int sy, int rop);
    }
}
