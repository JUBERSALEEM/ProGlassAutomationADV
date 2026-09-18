// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/Splash/SplashView.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: Splash Screen — 100% Complete Architectural Rewrite
//
// Faithfully matches approved mockup (docs/splash_redesign_dark.png):
//   • 16 module tiles across 2 rows of 8 columns
//   • Central floating PGA glass shield with tilted dashed orbit ring
//   • High-contrast wordmark and "MODERN GLASS FABRICATION ERP" subtitle
//   • Glowing neon cyan pill progress bar
//   • Non-overlapping loading and status indicators
//
// PRESERVED (zero regression — App.xaml.cs & runtime depend on these):
//   • FadeOutAndClose(Action), RunLoginAsync(), ShowTermsGate()
//   • TermsAccepted / TermsResolved / SignedIn / HasAcceptedCurrentTerms
//   • TermsVersion, RecordAcceptance
//   • SplashView_Loaded / SplashView_Unloaded timers + cleanup
//   • Animated loading dots (DispatcherTimer)
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.AI.AIConfig;
using ProGlassAutomation.ViewModels.Login;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.Security;
using ProGlassAutomation.Services.Updates;
using ProGlassAutomation.Views.Login;
using System;
using ProGlassAutomation.Utilities;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace ProGlassAutomation.Views.Splash
{
    /// <summary>
    /// Splash window shown at application startup.
    /// Animates in smoothly while background initialization runs.
    /// </summary>
    public partial class SplashView : Window
    {
        private DispatcherTimer? _loadingDotsTimer;
        private int _loadingDotsCount;

        // ══════════════════════════════════════════════════════════════════
        //  FEATURE MODULE DATA — 16 modules matching approved mockup
        // ══════════════════════════════════════════════════════════════════

        public sealed record FeatureItem(
            string Icon,
            string Label,
            bool IsTextBadge = false,
            string LabelTextBadge = "");

        private static readonly IReadOnlyList<FeatureItem> _features = BuildFeatures();

        private static IReadOnlyList<FeatureItem> BuildFeatures()
        {
            static string Nav(string key, string fallback)
            {
                if (System.Windows.Application.Current?
                        .TryFindResource("Nav." + key) is string s
                    && !string.IsNullOrWhiteSpace(s))
                    return s;
                return fallback;
            }

            // Exactly the 16 core ERP modules shown in docs/splash_redesign_dark.png
            // Row 1: Dashboard, Sheet Store, Specifications, Inquiry, Proforma, Job Order, Tax Invoice, Production
            // Row 2: Optimization, Dispatch, Statements, Reports, Job Profitability, VAT, Master Data, System
            return new[]
            {
                // Row 1 (8 modules)
                new FeatureItem("\uE80F", Nav("Dashboard",          "Dashboard")),
                new FeatureItem("\uE838", Nav("SheetStore",         "Sheet Store")),
                new FeatureItem("\uE7C3", Nav("Specifications",     "Specifications")),
                new FeatureItem("\uE721", Nav("Inquiry",            "Inquiry")),
                new FeatureItem("\uE70F", Nav("ProformaInvoice",    "Proforma")),
                new FeatureItem("\uE9D9", Nav("JobOrder",           "Job Order")),
                new FeatureItem("\uE8C7", Nav("TaxInvoice",         "Tax Invoice")),
                new FeatureItem("\uE8C6", Nav("ProductionItems",    "Production")),

                // Row 2 (8 modules)
                new FeatureItem("\uE9CE", Nav("Optimization",       "Optimization")),
                new FeatureItem("\uE7F4", Nav("Dispatch",           "Dispatch")),
                new FeatureItem("\uE8F7", Nav("StatementGenerator", "Statements")),
                new FeatureItem("\uE9F9", Nav("Reports",            "Reports")),
                new FeatureItem("\uE9D2", Nav("JobProfitability",   "Job Profitability")),
                new FeatureItem("",       Nav("Vat",                "VAT"), IsTextBadge: true, LabelTextBadge: "VAT"),
                new FeatureItem("\uE8B7", Nav("MasterData",         "Master Data")),
                new FeatureItem("\uE713", Nav("System",             "System")),
            };
        }

        public SplashView()
        {
            InitializeComponent();

            ApplyConfiguredSplashStyle();

            FeaturePanel.ItemsSource = _features;

            TermsBar.DataContext = _termsVm;
            _termsVm.RequestContinue = () =>
            {
                RecordAcceptance();
                TermsPanel.Visibility = Visibility.Collapsed;
                TermsResolved?.Invoke();
            };
            _termsVm.RequestDecline = () =>
            {
                TermsResolved?.Invoke();
                Close();
            };

            Loaded += SplashView_Loaded;
            Unloaded += SplashView_Unloaded;
        }

        private void ApplyConfiguredSplashStyle()
        {
            try
            {
                var cfg = App.Config;
                int style = ProGlassAutomation.Services.Splash.SplashThemeManager.ResolveActiveStyle(cfg);
                var (top, mid, bottom, accent) = ProGlassAutomation.Services.Splash.SplashThemeManager.GetPaletteForStyle(style);

                var bg = new LinearGradientBrush
                {
                    StartPoint = new Point(0.05, 0),
                    EndPoint = new Point(0.85, 1)
                };
                bg.GradientStops.Add(new GradientStop(top, 0.0));
                bg.GradientStops.Add(new GradientStop(mid, 0.5));
                bg.GradientStops.Add(new GradientStop(bottom, 1.0));
                bg.Freeze();

                SplashStageRoot.Background = bg;

                var accentBrush = new SolidColorBrush(accent);
                accentBrush.Freeze();

                Resources["AccentCyan"] = accentBrush;
                Resources["SplashCerulean"] = accentBrush;
                Resources["SplashCeruleanLite"] = accentBrush;
                Resources["SplashCeruleanDark"] = accentBrush;
            }
            catch (Exception ex)
            {
                AppLogService.Debug("[SplashView] ApplyConfiguredSplashStyle fallback: " + ex.Message);
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  LOADED — animation sequence (~2.5s)
        // ══════════════════════════════════════════════════════════════════

        private void SplashView_Loaded(object sender, RoutedEventArgs e)
        {
            // ─── 1. Logo settles in ──────────────────────────
            var logoScale = new DoubleAnimation
            {
                From = 0.90,
                To = 1.0,
                BeginTime = TimeSpan.FromMilliseconds(100),
                Duration = TimeSpan.FromMilliseconds(550),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            LogoScale.BeginAnimation(ScaleTransform.ScaleXProperty, logoScale);
            LogoScale.BeginAnimation(ScaleTransform.ScaleYProperty, logoScale.Clone());

            var logoFade = new DoubleAnimation
            {
                From = 0,
                To = 1,
                BeginTime = TimeSpan.FromMilliseconds(100),
                Duration = TimeSpan.FromMilliseconds(550),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            LogoBorder.BeginAnimation(OpacityProperty, logoFade);

            // ─── 2. App name fade + gentle slide ─────────────
            var nameFade = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(450),
                BeginTime = TimeSpan.FromMilliseconds(350)
            };
            var nameSlide = new DoubleAnimation
            {
                From = 14,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(450),
                BeginTime = TimeSpan.FromMilliseconds(350),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            AppNameText.BeginAnimation(OpacityProperty, nameFade);
            AppNameTranslate.BeginAnimation(TranslateTransform.YProperty, nameSlide);

            // ─── 3. Subtitle / Tagline fade ──────────────────
            FadeIn(TaglineText, fromMs: 550, durationMs: 400);

            // ─── 4. 16 Module tiles fade in ──────────────────
            FadeIn(FeaturePanel, fromMs: 750, durationMs: 500);

            // ─── 5. Neon cyan pill progress bar ──────────────
            double track = ProgressTrack.ActualWidth;
            if (track < 100) track = 640;
            var progressAnim = new DoubleAnimation
            {
                From = 0,
                To = track,
                Duration = TimeSpan.FromMilliseconds(2000),
                BeginTime = TimeSpan.FromMilliseconds(650),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
            };
            ProgressFill.BeginAnimation(WidthProperty, progressAnim);

            // ─── 6. Loading status & animated dots ───────────
            FadeIn(LoadingText, fromMs: 800, durationMs: 350);
            FadeIn(LoadingDots, fromMs: 800, durationMs: 350);
            StartLoadingDotsAnimation();
        }

        private void SplashView_Unloaded(object sender, RoutedEventArgs e)
        {
            StopLoadingIndicators();
        }

        private static void FadeIn(UIElement element, int fromMs, int durationMs)
        {
            var fade = new DoubleAnimation
            {
                From = 0,
                To = 1,
                BeginTime = TimeSpan.FromMilliseconds(fromMs),
                Duration = TimeSpan.FromMilliseconds(durationMs),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };
            element.BeginAnimation(OpacityProperty, fade);
        }

        private void StartLoadingDotsAnimation()
        {
            _loadingDotsTimer?.Stop();
            _loadingDotsCount = 0;
            _loadingDotsTimer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromMilliseconds(400)
            };
            _loadingDotsTimer.Tick += (_, _) =>
            {
                _loadingDotsCount = (_loadingDotsCount + 1) % 4;
                LoadingDots.Text = new string('.', _loadingDotsCount);
            };
            _loadingDotsTimer.Start();
        }

                // ══════════════════════════════════════════════════════════════════
        //  TERMS & CONDITIONS GATE
        // ══════════════════════════════════════════════════════════════════

        public const string TermsVersion = "2026-08-UAE-1";
        private readonly TermsGateViewModel _termsVm = new();

        /// <summary>True after the user explicitly accepted the terms (forwarded from TermsGateViewModel).</summary>
        public bool TermsAccepted => _termsVm.TermsAccepted;

        public event Action? TermsAcceptedEvent;
        public event Action? TermsResolved;
        public bool SignedIn { get; private set; }

        private static string TermsMarkerPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ProGlassAutomation", "terms-accepted.txt");

        public static bool HasAcceptedCurrentTerms()
        {
            try
            {
                if (!File.Exists(TermsMarkerPath)) return false;
                foreach (var line in File.ReadAllLines(TermsMarkerPath))
                    if (line.StartsWith("version=", StringComparison.Ordinal) &&
                        line["version=".Length..].Trim() == TermsVersion)
                        return true;
                return false;
            }
            catch
            {
                return false;
            }
        }

        private void RecordAcceptance()
        {
            try
            {
                var dir = Path.GetDirectoryName(TermsMarkerPath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                File.WriteAllText(
                    TermsMarkerPath,
                    $"version={TermsVersion}" + Environment.NewLine +
                    $"acceptedUtc={DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}Z" + Environment.NewLine +
                    $"machine={Environment.MachineName}" + Environment.NewLine +
                    $"windowsUser={Environment.UserName}" + Environment.NewLine);
            }
            catch (Exception ex)
            {
                AppLogService.Warning(
                    "Could not record terms acceptance: " + ExceptionMessages.Deep(ex),
                    new LogContext
                    {
                        Module = "Startup",
                        Class = nameof(SplashView),
                        Operation = "RecordAcceptance"
                    });
            }
        }

        public void ShowTermsGate()
        {
            StopLoadingIndicators();
            PosterHost.Visibility = Visibility.Collapsed;
            TermsPanel.Visibility = Visibility.Visible;
            FadeIn(TermsPanel, fromMs: 0, durationMs: 300);
        }

        public async Task<bool> RunLoginAsync()
        {
            var done = new TaskCompletionSource<bool>();

            void OnSignedIn()
            {
                LoginGate.SignedIn -= OnSignedIn;
                SignedIn = true;
                done.TrySetResult(true);
            }

            LoginGate.SignedIn += OnSignedIn;
            Closed += (_, _) => done.TrySetResult(false);

            StopLoadingIndicators();
            LoadingText.Text = "Please sign in to continue.";
            LoadingDots.Text = "";

            TermsPanel.Visibility = Visibility.Collapsed;
            PosterHost.Visibility = Visibility.Collapsed;
            LoginHost.Visibility = Visibility.Visible;
            FadeIn(LoginHost, fromMs: 0, durationMs: 300);

            try { Activate(); } catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[SplashView.xaml] RunLoginAsync: window may be closing: " + ExceptionMessages.Deep(ex)); }

            Dispatcher.BeginInvoke(new Action(() => LoginGate.FocusInitialFieldDeferred()),
                DispatcherPriority.Input);

            return await done.Task;
        }

        public Task<bool> RunFirstRunSetupAsync()
        {
            var done = new TaskCompletionSource<bool>();

            async void OnCreated(string newUsername, string newPassword)
            {
                SetupGate.Created -= OnCreated;
                SetupGate.IsEnabled = false;
                LoadingText.Text = "Opening workspace…";

                try
                {
                    if (App.Config != null)
                    {
                        App.Config.LastUsername = newUsername;
                        App.Config.RememberUsername = true;
                        ConfigService.Save(App.Config);
                    }
                }
                catch (System.Exception ex)
                {
                    ProGlassAutomation.Services.Logging.AppLogService.Debug("[SplashView] Remember-username persist failed: " + ExceptionMessages.Deep(ex));
                }

                try
                {
                    var (ok, error) = await Task.Run(
                        () => AuditService.SwitchUserAsync(newUsername, newPassword));

                    if (!ok)
                    {
                        AppLogService.Error(
                            "First-run silent sign-in failed for '" + newUsername +
                            "' immediately after CreateUserAsync: " + error, null,
                            new LogContext
                            {
                                Module = "Auth",
                                Class = nameof(SplashView),
                                Operation = "RunFirstRunSetupAsync"
                            });
                        SetupHost.Visibility = Visibility.Collapsed;
                        SetupGate.IsEnabled = true;
                        LoadingText.Text = "Please sign in to continue.";
                        bool loggedIn = await RunLoginAsync();
                        done.TrySetResult(loggedIn);
                        return;
                    }

                    AuditService.LogSessionBoundary(login: true);
                    SignedIn = true;
                    done.TrySetResult(true);
                }
                catch (Exception ex)
                {
                    AppLogService.Error(
                        "First-run silent sign-in threw unexpectedly.", ex,
                        new LogContext
                        {
                            Module = "Auth",
                            Class = nameof(SplashView),
                            Operation = "RunFirstRunSetupAsync"
                        });
                    done.TrySetResult(false);
                }
            }

            SetupGate.Created += OnCreated;
            Closed += (_, __) => done.TrySetResult(false);

            StopLoadingIndicators();
            LoadingText.Text = "Setting up the first administrator";
            LoadingDots.Text = "";

            TermsPanel.Visibility = Visibility.Collapsed;
            PosterHost.Visibility = Visibility.Collapsed;
            SetupHost.Visibility = Visibility.Visible;
            FadeIn(SetupHost, fromMs: 0, durationMs: 280);

            try { Activate(); } catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[SplashView.xaml] RunFirstRunSetupAsync: window may be closing: " + ExceptionMessages.Deep(ex)); }
            Dispatcher.BeginInvoke(new Action(() => SetupGate.Focus()),
                DispatcherPriority.Input);

            return done.Task;
        }

        public void FadeOutAndClose(Action onCompleted)
        {
            StopLoadingIndicators();
            IsHitTestVisible = false;

            var fadeOut = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(400),
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };

            fadeOut.Completed += (s, e) =>
            {
                onCompleted?.Invoke();
                Close();
            };

            BeginAnimation(OpacityProperty, fadeOut);
        }

        private void StopLoadingIndicators()
        {
            _loadingDotsTimer?.Stop();
            _loadingDotsTimer = null;
        }
    }
}
