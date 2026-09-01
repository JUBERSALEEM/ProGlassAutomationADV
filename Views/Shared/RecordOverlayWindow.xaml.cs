using ProGlassAutomation.Agent.Demo.Recording;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.ViewModels.Shared;
using System;
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
    ///
    /// 2026-09-02 (Phase 3.7): all HUD state + actions live in
    /// RecordOverlayViewModel (bound: time, badge, dot, pause glyph, stage,
    /// button visibilities, stop enablement; commands for pause/stop/
    /// open-folder/close). This file keeps only window-chrome concerns:
    /// the 250 ms DispatcherTimer (drives _vm.Tick() + Place()), auto-
    /// placement, the drag gesture, and the public API forwarders so
    /// DemoRecordingChrome is untouched.
    /// </summary>
    public partial class RecordOverlayWindow : Window
    {
        private readonly DispatcherTimer _timer;
        private readonly RecordOverlayViewModel _vm = new();
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

        public RecordOverlayWindow()
        {
            InitializeComponent();
            DataContext = _vm;

            _vm.RequestHide = () =>
            {
                try { Hide(); } catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); }
            };
            _vm.Finalized += () => _timer.Stop();

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
            _timer.Tick += (_, _) =>
            {
                _vm.Tick();
                Place();
            };
            Loaded += (_, _) => { _lastLeft = double.NaN; _lastTop = double.NaN; Place(); };
        }

        // ── Public API (DemoRecordingChrome contract) — forwarded ───────
        public void Bind(
            string module,
            WindowScreenRecorder recorder,
            Action? onStop,
            Action? onPause)
        {
            _vm.Bind(recorder, onStop, onPause);
            _timer.Start();
            Place();
        }

        public void SetStage(string message) => _vm.SetStage(message);

        public void SetFinal(string badge, string stats, string stage, bool failed, Color? dotColor = null)
            => _vm.SetFinal(badge, stats, stage, failed, dotColor);

        /// <summary>
        /// Wires the "open folder" HUD button to point at the just-saved
        /// file. Called from DemoRecordingChrome.ShowSaved so the user
        /// has a one-click affordance to reach the MP4 without the
        /// blocking MessageBox that used to fire.
        /// </summary>
        public void EnableOpenFolder(string? savedPath) => _vm.EnableOpenFolder(savedPath);

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
            catch (Exception ex)
            {
                // DragMove throws if the button was already released —
                // expected, not an error (logged so the audit can see it).
                AppLogService.Debug("[RecordOverlayWindow.xaml] DragMove: " + ex.Message);
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
