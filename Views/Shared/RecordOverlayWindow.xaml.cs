using ProGlassAutomation.Agent.Demo.Recording;
using ProGlassAutomation.Services.Logging;
using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace ProGlassAutomation.Views.Shared
{
    /// <summary>
    /// Compact recording card (12px card style, matching the app's other
    /// top-bar cards). A separate topmost window, so RenderTargetBitmap of
    /// MainWindow never includes the REC controls.
    ///
    /// Redesign notes:
    ///   • One card row — time on the left, then pause + stop. No stats,
    ///     audio, progress bar or stage rows in the always-visible chrome.
    ///   • The window is only re-positioned when it actually moves (the old
    ///     version moved it on every 200ms tick, which added jank).
    ///   • Time uses RecordingTime.Format — a plain MM:SS / H:MM:SS that no
    ///     longer overflows past 99 minutes.
    /// </summary>
    public partial class RecordOverlayWindow : Window
    {
        private readonly DispatcherTimer _timer;
        private WindowScreenRecorder? _recorder;
        private Action? _onStop;
        private Action? _onPause;
        private DateTime _started;
        private double _lastLeft = double.NaN;
        private double _lastTop = double.NaN;

        /// <summary>
        /// Once the user drags the HUD, stop auto-following the main window
        /// — the recorder stays where they put it.
        /// </summary>
        private bool _userMoved;

        public RecordOverlayWindow()
        {
            InitializeComponent();
            _timer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(250)
            };
            _timer.Tick += (_, _) => Tick();
            Loaded += (_, _) => { _lastLeft = double.NaN; _lastTop = double.NaN; Place(); };
        }

        public void Bind(
            string module,
            WindowScreenRecorder recorder,
            Action? onStop,
            Action? onPause)
        {
            _recorder = recorder;
            _onStop = onStop;
            _onPause = onPause;
            _started = DateTime.UtcNow;

            HudPause.Visibility = onPause == null ? Visibility.Collapsed : Visibility.Visible;
            HudStage.Text = "";
            HudStage.Visibility = Visibility.Collapsed;
            HudClose.Visibility = Visibility.Collapsed;
            HudStop.Visibility = Visibility.Visible;

            _timer.Start();
            Tick();
            Place();
        }

        public void SetStage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                HudStage.Visibility = Visibility.Collapsed;
                return;
            }

            HudStage.Text = message;
            HudStage.Visibility = Visibility.Visible;
        }

        public void SetFinal(string badge, string stats, string stage, bool failed)
        {
            _timer.Stop();
            _recorder = null;

            HudBadge.Text = badge;
            HudDot.Fill = new SolidColorBrush(
                failed ? Color.FromRgb(239, 68, 68) : Color.FromRgb(34, 197, 94));

            // Final state is a single status line + a close button; the
            // full detail (path, frames, audio) is shown in the saved dialog.
            string summary = stats ?? "";
            if (!string.IsNullOrWhiteSpace(stage) &&
                !string.Equals(stage, summary, StringComparison.Ordinal))
            {
                summary = string.IsNullOrWhiteSpace(summary)
                    ? stage
                    : summary + "\n" + stage;
            }
            HudStage.Text = summary ?? "";
            HudStage.Visibility = string.IsNullOrWhiteSpace(summary)
                ? Visibility.Collapsed
                : Visibility.Visible;

            HudPause.Visibility = Visibility.Collapsed;
            HudStop.Visibility = Visibility.Collapsed;
            HudClose.Visibility = Visibility.Visible;
        }

        /// <summary>
        /// Wires the "open folder" HUD button to point at the just-saved
        /// file. Called from DemoRecordingChrome.ShowSaved so the user
        /// has a one-click affordance to reach the MP4 without the
        /// blocking MessageBox that used to fire.
        /// </summary>
        public void EnableOpenFolder(string? savedPath)
        {
            _savedPathForOpenFolder = savedPath;
            HudOpenFolder.Visibility = string.IsNullOrEmpty(savedPath)
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        private string? _savedPathForOpenFolder;

        private void OpenFolder_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var p = _savedPathForOpenFolder;
                if (string.IsNullOrEmpty(p)) return;
                if (File.Exists(p))
                {
                    // /select highlights the file in Explorer.
                    var psi = new ProcessStartInfo("explorer.exe",
                        "/select,\"" + p + "\"") { UseShellExecute = true };
                    Process.Start(psi);
                }
                else
                {
                    var dir = Path.GetDirectoryName(p);
                    if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                        Process.Start(new ProcessStartInfo(dir) { UseShellExecute = true });
                }
            }
            catch (Exception ex) { AppLogService.Warning("Suppressed exception: " + ex.Message); }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            try { Hide(); } catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); }
        }

        private void Place()
        {
            // After the user has dragged the recorder, respect that position
            // instead of snapping it back to the window's bottom edge.
            if (_userMoved) return;

            try
            {
                var main = Application.Current?.MainWindow;
                double left, top;
                if (main == null || !main.IsVisible)
                {
                    left = SystemParameters.WorkArea.Right - ActualWidth - 24;
                    top = SystemParameters.WorkArea.Bottom - ActualHeight - 24;
                }
                else
                {
                    var origin = main.PointToScreen(new Point(0, 0));
                    double srcW = PresentationSource.FromVisual(main)
                        ?.CompositionTarget?.TransformToDevice.M11 ?? 1;
                    left = origin.X / srcW + (main.ActualWidth - ActualWidth) / 2;
                    top = origin.Y / srcW + main.ActualHeight - ActualHeight - 28;
                }

                // Only move when the position actually changed — setting
                // Left/Top every tick was a hidden source of recording jank.
                if (Math.Abs(left - _lastLeft) > 0.5 || Math.Abs(top - _lastTop) > 0.5)
                {
                    _lastLeft = left;
                    _lastTop = top;
                    Left = left;
                    Top = top;
                }
            }
            catch { /* last position is fine */ }
        }

        private void Tick()
        {
            var rec = _recorder;
            var elapsed = rec != null ? rec.Elapsed : DateTime.UtcNow - _started;
            HudTime.Text = RecordingTime.Format(elapsed);

            bool paused = rec?.IsPaused == true;
            HudBadge.Text = rec == null ? "IDLE" : paused ? "PAUSED" : "REC";
            HudBadge.Foreground = paused
                ? new SolidColorBrush(Color.FromRgb(251, 191, 36))
                : new SolidColorBrush(Color.FromRgb(252, 165, 165));
            HudDot.Opacity = paused ? 0.4 : 1.0;

            // Pause button flips between pause (⏸) and resume (▶) glyphs.
            if (HudPause.Visibility == Visibility.Visible)
            {
                if (HudPause.Content is System.Windows.Controls.TextBlock pauseGlyph)
                    pauseGlyph.Text = paused ? "\uE768" : "\uE769";
            }

            // Only surface the encoder stage while it is meaningful; the
            // idle HUD stays a single quiet card.
            if (rec != null && !string.IsNullOrWhiteSpace(rec.EncodeStage))
                SetStage(rec.EncodeStage +
                    (rec.EncodePercent > 0 ? $"  {rec.EncodePercent}%" : ""));
            else if (rec == null)
                SetStage(HudStage.Text);

            // Follow the main window without the per-tick move cost.
            Place();
        }

        private void Pause_Click(object sender, RoutedEventArgs e)
        {
            try { _onPause?.Invoke(); } catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); }
            Tick();
        }

        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            try { _onStop?.Invoke(); } catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); }
        }

        // ══════════════════════════════════════════════════════════════
        //  DRAG — move the recorder HUD anywhere on the screen
        //
        //  The HUD is a borderless topmost window; DragMove() lets the user
        //  hold anywhere on the card (except the pause/stop/close buttons)
        //  and move it out of the way. After the first drag, the HUD no
        //  longer auto-follows the main window (see _userMoved).
        // ══════════════════════════════════════════════════════════════

        private void HudDrag_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // The pause / stop / close buttons must stay clickable.
            if (IsInsideButton(e.OriginalSource as DependencyObject)) return;

            if (e.ButtonState == MouseButtonState.Pressed)
            {
                try
                {
                    DragMove();
                }
                catch
                {
                    // DragMove throws if the button was already released.
                    return;
                }

                _userMoved = true;
            }
        }

        private static bool IsInsideButton(DependencyObject? d)
        {
            while (d != null)
            {
                if (d is System.Windows.Controls.Primitives.ButtonBase) return true;

                DependencyObject? parent = null;
                try { parent = VisualTreeHelper.GetParent(d); } catch { /* template boundary */ }
                if (parent == null && d is FrameworkElement fe && fe.TemplatedParent != null)
                    parent = fe.TemplatedParent;
                d = parent;
            }
            return false;
        }
    }
}
