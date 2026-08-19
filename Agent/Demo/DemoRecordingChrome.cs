using ProGlassAutomation.Agent.Demo.Recording;
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
            RunOnUi(() =>
            {
                var win = Ensure();
                if (!win.IsVisible) win.Show();
                win.SetFinal("SAVED",
                    Fmt(duration) + " · " + frames + " frames",
                    (audioNote ?? "") + "\n" + path,
                    failed: false);
                try
                {
                    MessageBox.Show(
                        "Recording saved.\n\n" +
                        "Duration: " + Fmt(duration) + "\n" +
                        "Frames: " + frames + "\n" +
                        "Audio: " + (string.IsNullOrWhiteSpace(audioNote) ? "none" : audioNote) + "\n\n" +
                        path,
                        SavedTitle,
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                catch { }
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
                catch { }
            });
        }

        public static void Hide()
        {
            _recorder = null;
            RunOnUi(() =>
            {
                try { _overlay?.Hide(); } catch { }
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
            catch { }
            return _overlay;
        }

        private static string Fmt(TimeSpan ts)
        {
            if (ts < TimeSpan.Zero) ts = TimeSpan.Zero;
            return string.Format("{0:00}:{1:00}.{2:0}",
                (int)ts.TotalMinutes, ts.Seconds, ts.Milliseconds / 100);
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
