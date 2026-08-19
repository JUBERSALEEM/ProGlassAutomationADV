using ProGlassAutomation.Agent.Demo.Recording;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace ProGlassAutomation.Agent.Demo
{
    /// <summary>
    /// Live recording HUD on the main window: REC / duration / frames /
    /// audio / STOP. Visible only while a demo is recording so the user
    /// can see that frames are actually being captured.
    /// </summary>
    public static class DemoRecordingChrome
    {
        private const string HudName = "ProGlassDemoRecordHud";

        private static DispatcherTimer? _timer;
        private static WindowScreenRecorder? _recorder;
        private static DateTime _started;
        private static string _module = "";
        private static Action? _onStop;

        public static void ShowRecording(
            string module,
            WindowScreenRecorder recorder,
            Action? onStop)
        {
            _recorder = recorder;
            _onStop = onStop;
            _module = module ?? "Demo";
            _started = DateTime.UtcNow;
            RunOnUi(() =>
            {
                var hud = EnsureHud();
                if (hud == null) return;
                hud.Visibility = Visibility.Visible;
                ApplyState("REC",
                    _module + " · capturing this window",
                    "Internal …  Mic …",
                    showStop: true);
                StartTimer();
            });
        }

        public static void UpdateStage(string message)
        {
            RunOnUi(() =>
            {
                var hud = FindHud();
                if (hud == null || hud.Visibility != Visibility.Visible) return;
                if (FindNamed<TextBlock>(hud, "HudStage") is TextBlock stage)
                    stage.Text = message ?? "";
            });
        }

        public static void ShowSaved(
            string path, TimeSpan duration, int frames, string audioNote)
        {
            StopTimer();
            _recorder = null;
            RunOnUi(() =>
            {
                var hud = EnsureHud();
                if (hud != null)
                {
                    hud.Visibility = Visibility.Visible;
                    ApplyState("SAVED",
                        Fmt(duration) + " · " + frames + " frames",
                        audioNote ?? "",
                        showStop: false);
                }
                try
                {
                    MessageBox.Show(
                        "Recording saved.\n\n" +
                        "Duration: " + Fmt(duration) + "\n" +
                        "Frames: " + frames + "\n" +
                        "Audio: " + (string.IsNullOrWhiteSpace(audioNote) ? "none" : audioNote) + "\n\n" +
                        path,
                        "Demo recording saved",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
                catch { /* status HUD still visible */ }
            });
        }

        public static void ShowFailed(string reason)
        {
            StopTimer();
            _recorder = null;
            RunOnUi(() =>
            {
                var hud = EnsureHud();
                if (hud != null)
                {
                    hud.Visibility = Visibility.Visible;
                    ApplyState("FAILED",
                        reason ?? "Recording failed.",
                        "",
                        showStop: false);
                }
                try
                {
                    MessageBox.Show(
                        string.IsNullOrWhiteSpace(reason)
                            ? "The demo did not produce a valid recording."
                            : reason,
                        "Demo recording failed",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
                catch { /* HUD still visible */ }
            });
        }

        public static void Hide()
        {
            StopTimer();
            _recorder = null;
            RunOnUi(() =>
            {
                var hud = FindHud();
                if (hud != null) hud.Visibility = Visibility.Collapsed;
            });
        }

        private static void StartTimer()
        {
            StopTimer();
            var disp = Application.Current?.Dispatcher;
            if (disp == null) return;
            _timer = new DispatcherTimer(DispatcherPriority.Background, disp)
            {
                Interval = TimeSpan.FromMilliseconds(250)
            };
            _timer.Tick += (_, _) => Tick();
            _timer.Start();
            Tick();
        }

        private static void StopTimer()
        {
            try { _timer?.Stop(); } catch { }
            _timer = null;
        }

        private static void Tick()
        {
            var rec = _recorder;
            var elapsed = rec != null && rec.IsRecording
                ? DateTime.UtcNow - rec.StartedUtc
                : DateTime.UtcNow - _started;
            int frames = rec?.EncodedFrames ?? 0;
            string audio = AudioLine(rec);
            ApplyState("REC",
                Fmt(elapsed) + " · " + frames + " frames · " + _module,
                audio,
                showStop: true);
        }

        private static string AudioLine(WindowScreenRecorder? rec)
        {
            if (rec == null) return "Audio unknown";
            string inner = rec.LiveAudioStarted ? "Internal ON" : "Internal OFF";
            string mic = rec.MicAudioStarted ? "Mic ON" : "Mic OFF";
            if (!string.IsNullOrWhiteSpace(rec.AudioError))
                return inner + "  " + mic + "  (" + rec.AudioError + ")";
            return inner + "  " + mic;
        }

        private static string Fmt(TimeSpan ts)
        {
            if (ts < TimeSpan.Zero) ts = TimeSpan.Zero;
            return string.Format("{0:00}:{1:00}.{2:0}",
                (int)ts.TotalMinutes, ts.Seconds, ts.Milliseconds / 100);
        }

        private static void ApplyState(
            string badge, string stage, string audio, bool showStop)
        {
            var hud = FindHud();
            if (hud == null) return;
            if (FindNamed<TextBlock>(hud, "HudBadge") is TextBlock b)
                b.Text = badge;
            if (FindNamed<TextBlock>(hud, "HudStage") is TextBlock s)
                s.Text = stage;
            if (FindNamed<TextBlock>(hud, "HudAudio") is TextBlock a)
                a.Text = audio;
            if (FindNamed<Button>(hud, "HudStop") is Button stop)
                stop.Visibility = showStop ? Visibility.Visible : Visibility.Collapsed;
            if (FindNamed<Ellipse>(hud, "HudDot") is Ellipse dot)
                dot.Opacity = string.Equals(badge, "REC", StringComparison.Ordinal) ? 1 : 0.35;
        }

        private static Border? EnsureHud()
        {
            var win = Application.Current?.MainWindow;
            if (win == null) return null;
            return FindHud() ?? Create(win);
        }

        private static Border? FindHud()
        {
            var win = Application.Current?.MainWindow;
            if (win?.Content is Panel panel)
            {
                foreach (var child in panel.Children)
                    if (child is Border b && b.Name == HudName) return b;
            }
            return null;
        }

        private static T? FindNamed<T>(DependencyObject root, string name) where T : FrameworkElement
        {
            if (root is T match && match.Name == name) return match;
            int n = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < n; i++)
            {
                var hit = FindNamed<T>(VisualTreeHelper.GetChild(root, i), name);
                if (hit != null) return hit;
            }
            return null;
        }

        private static Border Create(Window win)
        {
            var dot = new Ellipse
            {
                Name = "HudDot",
                Width = 10,
                Height = 10,
                Margin = new Thickness(0, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            dot.SetResourceReference(Shape.FillProperty, "DangerBrush");

            var badge = new TextBlock
            {
                Name = "HudBadge",
                Text = "IDLE",
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0)
            };
            badge.SetResourceReference(TextBlock.ForegroundProperty, "OnAccentForegroundBrush");

            var stage = new TextBlock
            {
                Name = "HudStage",
                Text = "Not recording",
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            stage.SetResourceReference(TextBlock.ForegroundProperty, "OnAccentForegroundBrush");

            var audio = new TextBlock
            {
                Name = "HudAudio",
                Text = "",
                FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 12, 0)
            };
            audio.SetResourceReference(TextBlock.ForegroundProperty, "OnAccentForegroundBrush");

            var stop = new Button
            {
                Name = "HudStop",
                Content = "STOP",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Padding = new Thickness(12, 4, 12, 4),
                MinWidth = 64,
                Cursor = System.Windows.Input.Cursors.Hand
            };
            stop.SetResourceReference(Control.ForegroundProperty, "OnAccentForegroundBrush");
            stop.SetResourceReference(Control.BackgroundProperty, "DangerBrush");
            stop.Click += (_, _) =>
            {
                try { _onStop?.Invoke(); } catch { }
            };

            var row = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };
            row.Children.Add(dot);
            row.Children.Add(badge);
            row.Children.Add(stage);
            row.Children.Add(audio);
            row.Children.Add(stop);

            var hud = new Border
            {
                Name = HudName,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 56, 0, 0),
                Padding = new Thickness(14, 8, 14, 8),
                CornerRadius = new CornerRadius(12),
                Visibility = Visibility.Collapsed,
                Child = row
            };
            hud.SetResourceReference(Border.BackgroundProperty, "AccentBrush");
            Panel.SetZIndex(hud, 500);

            if (win.Content is Panel panel)
                panel.Children.Add(hud);
            return hud;
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
