using ProGlassAutomation.Agent.Demo.Recording;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Views.Shared;
using System;
using System.Windows;
using System.Windows.Threading;

namespace ProGlassAutomation.Agent.Demo
{
    /// <summary>
    /// Recording HUD as a SEPARATE topmost window. It is never a child
    /// of MainWindow, so RenderTargetBitmap of the app window cannot
    /// include STOP / PAUSE / duration.
    /// </summary>
    public static class DemoRecordingChrome
    {
        private static RecordOverlayWindow? _overlay;

        /// <summary>
        /// Set by the overlay's Closed event. A WPF Window cannot be
        /// re-Shown after Close() — Show() throws — so the cached instance
        /// must be discarded, not reused.
        /// </summary>
        private static bool _overlayClosed = true;
        private static WindowScreenRecorder? _recorder;
        private static string _kind = "Demo";

        public static void ShowRecording(
            string module,
            WindowScreenRecorder recorder,
            Action? onStop,
            string kind = "Demo")
        {
            _recorder = recorder;
            _kind = string.IsNullOrWhiteSpace(kind) ? "Demo" : kind.Trim();
            RunOnUi(() =>
            {
                var win = Ensure();
                win.Bind(module ?? "Demo", recorder, onStop,
                    IsUserKind ? () => TogglePause() : null);
                if (!win.IsVisible) win.Show();
            });
        }

        public static void UpdateStage(string message)
        {
            RunOnUi(() => _overlay?.SetStage(message ?? ""));
        }

        public static void ShowSaved(
            string path, TimeSpan duration, int frames, string audioNote)
        {
            _recorder = null;

            // Senior audit (Aug 2026): the modal MessageBox that used
            // to fire here BLOCKED the UI thread until the user
            // dismissed it — the user reported the recorder "keeps
            // running after the video is recorded". The HUD now
            // auto-dismisses after a short window; the toast on the
            // HUD carries the filename so no dialog is needed. The
            // "Open folder" button on the HUD close row still lets
            // the user reach the file.
            RunOnUi(() =>
            {
                var win = Ensure();
                if (!win.IsVisible) win.Show();
                win.SetFinal(
                    "SAVED",
                    RecordingTime.Format(duration) + " · " + frames + " frames",
                    (string.IsNullOrWhiteSpace(audioNote) ? "" : audioNote + "\n") +
                        System.IO.Path.GetFileName(path),
                    failed: false);
                win.EnableOpenFolder(path);

                // Auto-dismiss unless the user explicitly disabled it
                // in Settings (RecordingPreferences.AutoCloseHudAfterSave).
                bool autoClose = true;
                try
                {
                    autoClose = ProGlassAutomation.App.Config?.Recording?
                        .AutoCloseHudAfterSave ?? true;
                }
                catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[DemoRecordingChrome] ShowSaved: stick with default true: " + ex.Message); }

                if (autoClose)
                {
                    // 4-second dwell so the operator can read the toast,
                    // then close cleanly. Fire-and-forget timer.
                    var timer = new DispatcherTimer
                    {
                        Interval = TimeSpan.FromSeconds(4)
                    };
                    timer.Tick += (s, e) =>
                    {
                        timer.Stop();
                        try { _overlay?.Close(); } catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[DemoRecordingChrome] ShowSaved: best-effort operation failed: " + ex.Message); }
                        _overlay = null;
                    };
                    timer.Start();
                }
            });
        }

        public static void ShowFailed(string reason)
        {
            _recorder = null;
            RunOnUi(() =>
            {
                var win = Ensure();
                if (!win.IsVisible) win.Show();
                win.SetFinal("FAILED", reason ?? "Recording failed.", "", failed: true);

                // 2026-08-25 senior fix: the modal MessageBox that used to
                // fire here BLOCKED the UI thread — the same anti-pattern
                // already removed from ShowSaved. The HUD carries the
                // reason; the log keeps it for diagnostics.
                AppLogService.Warning(
                    (IsUserKind ? "Screen recording failed: " : "Demo recording failed: ") +
                    (reason ?? "(no reason)"));
            });
        }

        /// <summary>
        /// Terminal HUD state when the user pressed STOP. Not a failure —
        /// amber badge, no dialog. 2026-08-25: before this existed the
        /// HUD stayed stuck on "REC" after a cancel.
        /// </summary>
        public static void ShowCancelled(string reason)
        {
            _recorder = null;
            RunOnUi(() =>
            {
                var win = Ensure();
                if (!win.IsVisible) win.Show();
                win.SetFinal("STOPPED", reason ?? "Recording stopped.", "",
                    failed: false,
                    dotColor: System.Windows.Media.Color.FromRgb(251, 191, 36));
            });
        }

        public static void Hide()
        {
            _recorder = null;
            RunOnUi(() =>
            {
                try { _overlay?.Hide(); } catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); }
            });
        }

        private static void TogglePause()
        {
            var rec = _recorder;
            if (rec == null || !rec.IsRecording) return;
            rec.SetPaused(!rec.IsPaused);
        }

        private static bool IsUserKind =>
            string.Equals(_kind, "Screen", StringComparison.OrdinalIgnoreCase);

        private static string SavedTitle =>
            IsUserKind ? "Screen recording saved" : "Demo recording saved";

        private static string FailedTitle =>
            IsUserKind ? "Screen recording failed" : "Demo recording failed";

        private static RecordOverlayWindow Ensure()
        {
            // A WPF Window cannot be re-Shown once Closed — Show() throws
            // InvalidOperationException. ShowSaved's auto-dismiss timer
            // closes the overlay but the cached reference survived, so the
            // NEXT recording tried to reuse a dead window: the HUD then
            // failed to appear, or appeared stuck, and the old one was
            // never released. Detect the closed window and rebuild.
            if (_overlay != null && !_overlayClosed) return _overlay;

            _overlay = new RecordOverlayWindow();
            _overlayClosed = false;

            // Authoritative signal. Inferring "closed" from IsLoaded /
            // PresentationSource is unreliable — those look identical for a
            // window that has merely never been shown — so the window tells
            // us directly. Also covers the user clicking the HUD's own X.
            _overlay.Closed += (_, _) =>
            {
                _overlayClosed = true;
                _overlay = null;
            };
            try
            {
                var main = Application.Current?.MainWindow;
                if (main != null) _overlay.Owner = main;
            }
            catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); }
            return _overlay;
        }

        private static void RunOnUi(Action work)
        {
            var disp = Application.Current?.Dispatcher;
            if (disp == null) return;
            if (disp.CheckAccess()) work();
            else disp.Invoke(work);
        }
    }
}
