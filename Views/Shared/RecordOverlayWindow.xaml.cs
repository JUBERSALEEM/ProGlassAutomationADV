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

        // True while a drag is in flight. DragMove() runs a NESTED message
        // loop, so the 250 ms DispatcherTimer keeps ticking inside it —
        // Tick()/Place() must not reposition the window mid-drag.
        private bool _isDragging;

        // Frozen brushes reused by every Tick — the old code allocated two
        // new SolidColorBrushes every 250 ms for the whole recording.
        private static readonly SolidColorBrush BadgePausedBrush = Frozen(Color.FromRgb(251, 191, 36));
        private static readonly SolidColorBrush BadgeRecBrush = Frozen(Color.FromRgb(252, 165, 165));
        private static readonly SolidColorBrush DotFailBrush = Frozen(Color.FromRgb(239, 68, 68));
        private static readonly SolidColorBrush DotOkBrush = Frozen(Color.FromRgb(34, 197, 94));
        private static readonly SolidColorBrush DotStopBrush = Frozen(Color.FromRgb(251, 191, 36));

        private static SolidColorBrush Frozen(Color c)
        {
            var b = new SolidColorBrush(c);
            b.Freeze();
            return b;
        }

        public RecordOverlayWindow()
        {
            InitializeComponent();

            // 2026-08-25 senior fix — the HUD clock stayed at 0:00 during
            // recordings. The timer ran at DispatcherPriority.Background,
            // the LOWEST priority — while a demo is recording the UI
            // thread has a constant stream of higher-priority work
            // (captures, workflow dispatch, animations), so the tick was
            // starved and the elapsed time never visibly advanced.
            // Render priority ticks reliably yet still yields to input.
            _timer = new DispatcherTimer(DispatcherPriority.Render)
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
            HudStop.IsEnabled = true;   // re-arm after a previous stop/cancel

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

        public void SetFinal(string badge, string stats, string stage, bool failed, Color? dotColor = null)
        {
            _timer.Stop();
            _recorder = null;

            HudBadge.Text = badge;
            HudDot.Fill = dotColor != null
                ? Frozen(dotColor.Value)
                : (failed ? DotFailBrush : DotOkBrush);

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
            // 2026-08-25: also stand down while a drag is in flight — the
            // timer keeps ticking inside DragMove's nested message loop.
            if (_userMoved || _isDragging) return;

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
            catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[RecordOverlayWindow.xaml] Place: last position is fine: " + ex.Message); }
        }

        private void Tick()
        {
            var rec = _recorder;
            var elapsed = rec != null ? rec.Elapsed : DateTime.UtcNow - _started;
            HudTime.Text = RecordingTime.Format(elapsed);

            bool paused = rec?.IsPaused == true;
            HudBadge.Text = rec == null ? "IDLE" : paused ? "PAUSED" : "REC";
            HudBadge.Foreground = paused ? BadgePausedBrush : BadgeRecBrush;
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
            // 2026-08-25 senior fix: immediate feedback. The teardown
            // (final encodes, audio close, MP4 pack) legitimately takes a
            // few seconds; without this the HUD looked dead after STOP.
            HudStop.IsEnabled = false;
            SetStage("Stopping…");

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

            if (e.ButtonState != MouseButtonState.Pressed) return;

            // 2026-08-25 senior fix — the laggy/fighting drag:
            // _userMoved was only set AFTER DragMove() returned. But
            // DragMove() runs a NESTED message loop, so the 250 ms timer
            // kept firing INSIDE the drag and Place() kept snapping the
            // HUD back toward the main window — the window literally
            // fought the user's hand, which reads as heavy lag. Both
            // flags are set BEFORE the drag so Place() stands down for
            // the whole gesture.
            _userMoved = true;
            _isDragging = true;
            try
            {
                DragMove();
            }
            catch
            {
                // DragMove throws if the button was already released.
            }
            finally
            {
                _isDragging = false;
            }
        }

        private static bool IsInsideButton(DependencyObject? d)
        {
            while (d != null)
            {
                if (d is System.Windows.Controls.Primitives.ButtonBase) return true;

                DependencyObject? parent = null;
                try { parent = VisualTreeHelper.GetParent(d); } catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[RecordOverlayWindow.xaml] IsInsideButton: template boundary: " + ex.Message); }
                if (parent == null && d is FrameworkElement fe && fe.TemplatedParent != null)
                    parent = fe.TemplatedParent;
                d = parent;
            }
            return false;
        }
    }
}
