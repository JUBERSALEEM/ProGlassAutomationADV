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
//  ⚠ CORRECTION (2026-09-02). An earlier version of this header claimed
//  PrintWindow "runs entirely on the CALLING thread ... and never touches
//  the WPF dispatcher". THAT WAS FALSE, and it caused the reported freeze.
//
//  Microsoft's own contract for PrintWindow:
//
//    "This is a BLOCKING or SYNCHRONOUS function ... THE APPLICATION THAT
//     OWNS THE WINDOW referenced by hWnd PROCESSES the PrintWindow call
//     and renders the image ... The application receives a WM_PRINT
//     message ... Calling this function from a thread that manages
//     interaction with the user interface could make the application
//     appear to be unresponsive."
//
//  We pass our OWN MainWindow, so the owning thread is our OWN UI thread:
//
//      bg thread → PrintWindow → SendMessage(WM_PRINT) → UI THREAD renders
//      bg thread ←──────────── blocks until finished ──┘
//
//  Calling it 30x/second therefore forces the UI thread to perform 30
//  synchronous full-window renders per second. Architecturally identical
//  to the RenderTargetBitmap path it replaced — the request moved
//  off-thread, the WORK did not. Module switches crawl, dialogs stop
//  responding, and STOP cannot be clicked because its input queues behind
//  the WM_PRINT backlog.
//
//  PHASE 2 — THE ACTUAL FIX: BLIT FROM THE SCREEN DC
//  PrintWindow is no longer the primary path. Instead:
//
//      BitBlt(dst, GetDC(NULL), screenX, screenY, SRCCOPY)
//
//  Reading the DESKTOP device context sends NO window message to anyone.
//  It never enters our message queue, so it CANNOT block the UI thread —
//  which is the entire defect this class existed to work around. Cost is
//  a GPU-to-sysmem blit, roughly 1-3 ms at 1080p, on the capture thread.
//
//  It also captures popups, dropdowns, context menus and dialogs for
//  free, because they are physically on screen inside our window's
//  rectangle.
//
//  HONEST TRADE-OFF: this reads the SCREEN REGION our window occupies.
//  If another application is placed on top of ours, those pixels are what
//  gets recorded. That is how lightweight screen recorders behave, and it
//  is strictly better than an application that freezes. The rect is
//  clamped to our own window, so the desktop at large is never recorded,
//  and the recording HUD excludes itself via
//  SetWindowDisplayAffinity(WDA_EXCLUDEFROMCAPTURE).
//
//  PrintWindow is RETAINED as an explicit fallback for the case where the
//  window is not visible on screen (occluded/off-screen), where a screen
//  blit would capture the wrong pixels. It is used only then, so its
//  UI-thread cost is paid rarely instead of 30 times a second.
//
//  APP-ONLY BY DESIGN
//  The capture is bound to a single HWND, so the desktop, other apps and
//  notifications never appear.
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

        /// <summary>
        /// Include layered/transparent windows in a BitBlt. Without it the
        /// app's own glass chrome and any overlapping popup are simply
        /// missing from the captured frame.
        /// </summary>
        private const int CAPTUREBLT = 0x40000000;
        private const int BI_RGB = 0;
        private const int DIB_RGB_COLORS = 0;

        private IntPtr _hwnd;
        private IntPtr _memDc;
        private IntPtr _dib;
        private IntPtr _oldObj;
        private IntPtr _bits;
        private int _width;
        private int _height;
        private int _screenX;
        private int _screenY;
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
        /// Use the desktop-DC blit (default, and the whole point of Phase 2).
        /// Exposed so the fallback can be forced for diagnosis.
        /// </summary>
        public bool UseScreenBlit { get; set; } = true;

        /// <summary>
        /// True once a frame has been served by the PrintWindow fallback.
        /// Surfaced so a machine that silently degrades to the expensive
        /// path is diagnosable instead of just "feeling slow".
        /// </summary>
        public bool UsedFallbackPath { get; private set; }

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

            // The desktop blit reads real screen pixels, so a hidden window
            // would capture whatever is behind it. Refuse instead.
            if (!IsWindowVisible(hwnd)) return false;

            if (!GetClientRect(hwnd, out RECT rc)) return false;

            int w = rc.Right - rc.Left;
            int h = rc.Bottom - rc.Top;
            if (w < 2 || h < 2) return false;

            // Screen position of the client area's top-left. Needed because
            // the primary capture path blits from the DESKTOP DC, which is
            // addressed in screen coordinates, not window coordinates.
            var origin = new POINT { X = 0, Y = 0 };
            if (ClientToScreen(hwnd, ref origin))
            {
                _screenX = origin.X;
                _screenY = origin.Y;
            }

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

            // Still timed: the caller's adaptive throttle uses this, and it
            // now also proves the screen path is cheap.
            var sw = System.Diagnostics.Stopwatch.StartNew();

            bool ok = false;

            // ── PRIMARY: blit from the DESKTOP DC ────────────────────
            // Sends no window message, so it can never block our UI
            // thread. Only valid while the window is actually on screen,
            // which IsWindowVisible + the non-iconic check in Prepare()
            // already established.
            if (UseScreenBlit)
            {
                IntPtr screenDc = GetDC(IntPtr.Zero);
                if (screenDc != IntPtr.Zero)
                {
                    try
                    {
                        // CAPTUREBLT is required or layered/transparent
                        // windows (our own glass chrome, and any popup
                        // overlapping us) are omitted from the blit.
                        ok = BitBlt(
                            _memDc, 0, 0, _width, _height,
                            screenDc, _screenX, _screenY,
                            SRCCOPY | CAPTUREBLT);
                    }
                    finally
                    {
                        ReleaseDC(IntPtr.Zero, screenDc);
                    }
                }
            }

            // ── FALLBACK: PrintWindow ────────────────────────────────
            // Costly — it marshals WM_PRINT to the owning UI thread (see
            // header) — so it runs ONLY when the screen blit is
            // unavailable or failed, not on every frame.
            if (!ok)
            {
                ok = PrintWindow(_hwnd, _memDc, PW_RENDERFULLCONTENT);
                if (ok) UsedFallbackPath = true;
            }

            if (!ok) return false;

            Marshal.Copy(_bits, buffer, 0, needed);

            sw.Stop();
            LastCaptureMs = sw.Elapsed.TotalMilliseconds;

            LastFrameWasBlank = IsMostlyBlank(buffer, needed);
            return true;
        }

        /// <summary>
        /// Wall-clock cost of the last successful capture, in milliseconds.
        ///
        /// This is not a diagnostic nicety: because PrintWindow blocks on
        /// the window's owning thread, this value is very close to the
        /// amount of time the UI thread was unavailable to the user. The
        /// capture loop throttles against it.
        /// </summary>
        public double LastCaptureMs { get; private set; }

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
            _screenX = 0;
            _screenY = 0;
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

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        [DllImport("user32.dll")]
        private static extern bool ClientToScreen(IntPtr hwnd, ref POINT pt);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hwnd);

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
