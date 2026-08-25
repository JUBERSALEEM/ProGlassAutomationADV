// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/About/AppIconResource.cs
// PROJECT: ProGlassAutomation
// MODULE: About — shared app-icon bitmap
//
// 2026-08-25 freeze fix:
//   AboutView (hero) and DeveloperSection (avatar) each declared their
//   own ImageBrush against
//   pack://application:,,,/Resources/proglass_icon_256.png — a 2 MB,
//   1254×1254 PNG. The About page is re-materialised on EVERY
//   navigation (DataTemplate in MainWindow.xaml), so the PNG was
//   decoded twice on the UI thread on every open of the page — one of
//   the remaining costs behind the "about page freeze" on weaker
//   machines. This holder decodes the bitmap exactly ONCE per process
//   and freezes it, so every ImageBrush shares one decoded bitmap and
//   cross-thread use is safe.
//
//   The property NEVER throws: on a load failure it returns null
//   (ImageBrush renders blank) instead of breaking the About XAML
//   load, and it does not retry a failed load (one bad PNG must not
//   cost a re-decode attempt on every property read).
//
//   NOTE (2026-08-25 build fix): WPF's ImageSource has no static
//   "Empty" member — the first version of this file used one and
//   failed with CS0117 (and cascaded into a phantom CS1061 on
//   App.InitializeComponent via the stale generated App.g.cs). The
//   sentinel is now a plain null + a _loadFailed flag.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ProGlassAutomation.Views.About
{
    /// <summary>
    /// Process-lifetime cache of the app icon bitmap. Referenced from
    /// XAML via <c>{x:Static viewsAbout:AppIconResource.Icon}</c>.
    /// Returns null (never throws) when the image cannot be loaded —
    /// the ImageBrush then simply renders blank.
    /// </summary>
    public static class AppIconResource
    {
        private static readonly object _gate = new();

        // null = "not loaded yet". A successful load stores the
        // frozen bitmap here for the rest of the process.
        private static ImageSource? _icon;

        // Sticky flag: once the load has failed, stop retrying. A
        // corrupt / missing resource must not cost a decode attempt
        // on every access.
        private static volatile bool _loadFailed;

        /// <summary>
        /// The app icon as a single shared, frozen BitmapImage.
        /// Decoded once per process; safe from any thread.
        /// </summary>
        public static ImageSource? Icon
        {
            get
            {
                var existing = Volatile.Read(ref _icon);
                if (existing != null) return existing;
                if (_loadFailed) return null;

                lock (_gate)
                {
                    existing = _icon;
                    if (existing != null) return existing;
                    if (!_loadFailed)
                    {
                        try
                        {
                            var bmp = new BitmapImage();
                            bmp.BeginInit();
                            bmp.UriSource = new Uri(
                                "pack://application:,,,/Resources/proglass_icon_256.png",
                                UriKind.Absolute);
                            bmp.CacheOption = BitmapCacheOption.OnLoad;
                            bmp.CreateOptions =
                                BitmapCreateOptions.IgnoreColorProfile;
                            bmp.EndInit();
                            bmp.Freeze();
                            _icon = bmp;
                        }
                        catch
                        {
                            // Load failed (missing resource, corrupt
                            // file) — stay null. The icon renders
                            // blank; the page must still load.
                            _loadFailed = true;
                        }
                    }
                    return _icon;
                }
            }
        }
    }
}
