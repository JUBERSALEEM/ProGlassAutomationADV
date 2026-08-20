using ProGlassAutomation.Agent.Demo.Recording;
using ProGlassAutomation.Services.Logging;
using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace ProGlassAutomation.Views.Shared
{
    /// <summary>
    /// Separate topmost window. RenderTargetBitmap of MainWindow never
    /// includes this, so the REC controls stay off the video.
    /// </summary>
    public partial class RecordOverlayWindow : Window
    {
        private readonly DispatcherTimer _timer;
        private WindowScreenRecorder? _recorder;
        private Action? _onStop;
        private Action? _onPause;
        private DateTime _started;
        private string _module = "";

        public RecordOverlayWindow()
        {
            InitializeComponent();
            _timer = new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromMilliseconds(200)
            };
            _timer.Tick += (_, _) => Tick();
            Loaded += (_, _) => Place();
        }

        public void Bind(
            string module,
            WindowScreenRecorder recorder,
            Action? onStop,
            Action? onPause)
        {
            _module = module ?? "";
            _recorder = recorder;
            _onStop = onStop;
            _onPause = onPause;
            _started = DateTime.UtcNow;
            HudPause.Visibility = onPause == null ? Visibility.Collapsed : Visibility.Visible;
            _timer.Start();
            Tick();
            Place();
        }

        public void SetStage(string message)
        {
            HudStage.Text = message ?? "";
        }

        public void SetFinal(string badge, string stats, string stage, bool failed)
        {
            _timer.Stop();
            _recorder = null;
            HudBadge.Text = badge;
            HudStats.Text = stats;
            HudStage.Text = stage;
            HudPause.Visibility = Visibility.Collapsed;
            HudStop.Visibility = Visibility.Collapsed;
            // Let the user dismiss the finished HUD.
            HudClose.Visibility = Visibility.Visible;
            HudDot.Fill = failed
                ? new SolidColorBrush(Color.FromRgb(239, 68, 68))
                : new SolidColorBrush(Color.FromRgb(34, 197, 94));
            HudBar.Width = failed ? 0 : Math.Max(40, HudRoot.ActualWidth - 28);
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            try { Hide(); } catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); }
        }

        private void Place()
        {
            try
            {
                var main = Application.Current?.MainWindow;
                if (main == null || !main.IsVisible)
                {
                    Left = SystemParameters.WorkArea.Right - ActualWidth - 24;
                    Top = SystemParameters.WorkArea.Bottom - ActualHeight - 24;
                    return;
                }
                var origin = main.PointToScreen(new Point(0, 0));
                double srcW = PresentationSource.FromVisual(main)
                    ?.CompositionTarget?.TransformToDevice.M11 ?? 1;
                Left = origin.X / srcW + (main.ActualWidth - ActualWidth) / 2;
                Top = origin.Y / srcW + main.ActualHeight - ActualHeight - 28;
            }
            catch { /* last position is fine */ }
        }

        private void Tick()
        {
            var rec = _recorder;
            var elapsed = rec != null ? rec.Elapsed : DateTime.UtcNow - _started;
            HudTime.Text = Fmt(elapsed);

            bool paused = rec?.IsPaused == true;
            HudBadge.Text = rec == null ? "IDLE" : paused ? "PAUSED" : "REC";
            HudDot.Opacity = paused ? 0.35 : 1;
            HudPause.Content = paused ? "RESUME" : "PAUSE";

            int frames = rec?.EncodedFrames ?? 0;
            int dropped = rec?.DroppedFrames ?? 0;
            bool live = rec?.LiveEncodeActive == true;
            long bytes = rec?.LiveBytesPiped ?? 0;
            string size = bytes >= 1024 * 1024
                ? $"{bytes / (1024.0 * 1024.0):0.0} MB piped"
                : bytes > 0 ? $"{bytes / 1024.0:0} KB piped" : "waiting";
            HudStats.Text =
                $"{frames} frames · {dropped} dropped · " +
                (live ? "live encode · " + size : "JPEG fallback") +
                (string.IsNullOrEmpty(_module) ? "" : " · " + _module);

            string inner = rec == null ? "Internal …" : rec.LiveAudioStarted ? "Internal ON" : "Internal OFF";
            string mic = rec == null ? "Mic …" : rec.MicAudioStarted ? "Mic ON" : "Mic OFF";
            HudAudio.Text = string.IsNullOrWhiteSpace(rec?.AudioError)
                ? inner + "   " + mic
                : inner + "   " + mic + "   (" + rec!.AudioError + ")";

            if (!string.IsNullOrWhiteSpace(rec?.EncodeStage))
                HudStage.Text = rec!.EncodeStage +
                    (rec.EncodePercent > 0 ? $"  {rec.EncodePercent}%" : "");

            double barMax = Math.Max(40, HudRoot.ActualWidth - 28);
            if (rec != null && rec.EncodePercent > 0)
                HudBar.Width = barMax * Math.Clamp(rec.EncodePercent / 100.0, 0, 1);
            else
                HudBar.Width = paused ? barMax * 0.15 : Math.Min(barMax, 24 + (frames % 40));

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

        private static string Fmt(TimeSpan ts)
        {
            if (ts < TimeSpan.Zero) ts = TimeSpan.Zero;
            return string.Format("{0:00}:{1:00}.{2:0}",
                (int)ts.TotalMinutes, ts.Seconds, ts.Milliseconds / 100);
        }
    }
}
