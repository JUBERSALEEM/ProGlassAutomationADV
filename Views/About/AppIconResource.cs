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
//   The property NEVER throws: on any failure it falls back to
//   ImageSource.Empty (blank icon) instead of breaking the About XAML
//   load.
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
    /// </summary>
    public static class AppIconResource
    {
        private static readonly object _gate = new();

        // ImageSource.Empty is the "not yet loaded / load failed"
        // sentinel — it always renders as a blank icon and is itself
        // a frozen, thread-safe ImageSource.
        private static ImageSource _icon = ImageSource.Empty;

        /// <summary>
        /// The app icon as a single shared, frozen BitmapImage.
        /// Decoded once per process; safe from any thread.
        /// </summary>
        public static ImageSource Icon
        {
            get
            {
                var existing = Volatile.Read(ref _icon);
                if (!ReferenceEquals(existing, ImageSource.Empty))
                    return existing;

                lock (_gate)
                {
                    if (ReferenceEquals(_icon, ImageSource.Empty))
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
                            // file) — keep ImageSource.Empty. The icon
                            // renders blank; the page must still load.
                        }
                    }
                    return _icon;
                }
            }
        }
    }
}
