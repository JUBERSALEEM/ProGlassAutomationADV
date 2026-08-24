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
                catch { /* stick with default true */ }

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
                        try { _overlay?.Close(); } catch { /* silence */ }
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
                try
                {
                    MessageBox.Show(
                        string.IsNullOrWhiteSpace(reason)
                            ? (IsUserKind
                                ? "The screen recording did not produce a valid MP4."
                                : "The demo did not produce a valid recording.")
                            : reason,
                        FailedTitle,
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
                catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); }
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
            if (_overlay != null) return _overlay;
            _overlay = new RecordOverlayWindow();
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
