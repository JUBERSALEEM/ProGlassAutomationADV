// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/About/AboutView.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: About — tabbed shell (v6)
//
// The shell hosts the hero + tab strip and switches between the three
// section UserControls. The email contact handler lives in
// Sections/DeveloperSection.xaml.cs so that section stays self-contained.
//
// 2026-08-25 SENIOR HARDENING: construction is now wrapped. If ANY part
// of the About XAML fails to load on a given machine (resource lookup,
// font, image, section ctor), the app must NOT crash and the page must
// NOT render transparent/empty: the exact exception is shown on the
// page itself and written to the app log, so the real cause is finally
// visible instead of a freeze + crash dialog.
//
// 2026-09-10 CRASH FIX (ERR-D71810 / ERR-CDF43D) + FLUID MOTION:
// AboutView is instantiated inside a DataTemplate (MainWindow L147) and
// therefore loads through WPF's OPTIMISED template path
// (LoadOptimizedTemplateContent), which wraps the whole subtree in a
// single BeginInit/EndInit. ANY Storyboard placed inline in the visual
// tree — whether via <X.Triggers> OR an inline <X.Style> Style.Triggers —
// is a Freezable the loader tries to BeginInit while the subtree is
// already mid-init, throwing "Cannot have nested BeginInit calls on the
// same instance". The earlier attempt to move the loops into inline
// Styles did NOT help for exactly this reason. ALL animation is therefore
// now started imperatively from here, wrapped in try/catch so it can
// never crash the page.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Utilities;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ProGlassAutomation.Views.About
{
    public partial class AboutView : UserControl
    {
        public AboutView()
        {
            try
            {
                InitializeComponent();

                // Fluid motion is wired here (not in XAML) — see the crash
                // note in the file header. Never let animation setup crash
                // the page: the try/catch inside StartAmbientMotion guards it,
                // and the Loaded handler is the safe place to touch the tree.
                Loaded += OnLoaded;
            }
            catch (System.Exception ex)
            {
                try
                {
                    ProGlassAutomation.Services.Logging.AppLogService.Critical(
                        "AboutView failed to load", ex,
                        new ProGlassAutomation.Services.Logging.LogContext
                        {
                            Module = "About",
                            Operation = "Load",
                        });
                }
                catch { /* logging must never throw */ }

                try { Content = BuildFailurePanel(ex); }
                catch { /* the fallback itself must never throw */ }
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Run once — Loaded can fire again after unload/reload.
            Loaded -= OnLoaded;

            StartAmbientMotion();
            WireSectionReveals();
        }

        // ──────────────────────────────────────────────────────────────
        //  AMBIENT MOTION — continuous, buttery-smooth stage life.
        //  Every loop is slow, eased and AutoReverse for a calm premium
        //  feel; purely decorative, so the live theme/accent re-tint is
        //  untouched. All started imperatively (never inline XAML).
        // ──────────────────────────────────────────────────────────────
        private void StartAmbientMotion()
        {
            try
            {
                // SENIOR PERF GATE (2026-09-11): the app already publishes
                // its own effects budget (RichEffectsEnabled; the perf tier
                // logs "rich effects False" on constrained machines). Ten
                // forever-running transforms over a shadowed page re-render
                // every frame — the "About feels laggy" report. Decorative
                // motion is OFF unless the app says effects are affordable.
                if (Application.Current?.TryFindResource("RichEffectsEnabled")
                        is not true)
                {
                    SafeLog("StartAmbientMotion",
                        new System.InvalidOperationException(
                            "Rich effects disabled — ambient motion skipped."));
                    return;
                }

                // Orb A — drift + gentle swell.
                Loop(OrbATranslate, TranslateTransform.XProperty, 0, -30, 21);
                Loop(OrbATranslate, TranslateTransform.YProperty, 0, 24, 24);
                Loop(OrbAScale, ScaleTransform.ScaleXProperty, 1, 1.07, 17);
                Loop(OrbAScale, ScaleTransform.ScaleYProperty, 1, 1.07, 17);

                // Orb B — counter-drift + swell on offset periods.
                Loop(OrbBTranslate, TranslateTransform.XProperty, 0, 26, 23);
                Loop(OrbBTranslate, TranslateTransform.YProperty, 0, -20, 19);
                Loop(OrbBScale, ScaleTransform.ScaleXProperty, 1.05, 1, 20);
                Loop(OrbBScale, ScaleTransform.ScaleYProperty, 1.05, 1, 20);

                // Icon orbit ring — one slow, perfectly linear revolution.
                var spin = new DoubleAnimation
                {
                    From = 0,
                    To = 360,
                    Duration = TimeSpan.FromSeconds(60),
                    RepeatBehavior = RepeatBehavior.Forever,
                };
                IconRingRotate.BeginAnimation(RotateTransform.AngleProperty, spin);
            }
            catch (System.Exception ex)
            {
                // A missing element or animation quirk must never take the
                // page down — motion is decorative. Log and carry on.
                SafeLog("StartAmbientMotion", ex);
            }
        }

        /// <summary>
        /// Start a slow, sine-eased, auto-reversing forever loop on an
        /// animatable transform. No-op if the target is null.
        /// </summary>
        private static void Loop(Animatable? target, DependencyProperty prop,
                                 double from, double to, double seconds)
        {
            if (target is not IAnimatable animatable) return;
            var anim = new DoubleAnimation
            {
                From = from,
                To = to,
                Duration = TimeSpan.FromSeconds(seconds),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever,
                EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut },
            };
            // Decorative, not gameplay: 30 fps halves the invalidation
            // cost with zero perceived difference for drift/swell.
            Timeline.SetDesiredFrameRate(anim, 30);
            animatable.BeginAnimation(prop, anim);
        }

        // ──────────────────────────────────────────────────────────────
        //  SECTION REVEAL — each section fades + slides up gently as it
        //  becomes visible (SelectedTabIndex switch is instant to the
        //  click; the reveal reads as a polished cut-in). Driven from
        //  IsVisibleChanged instead of an inline Trigger storyboard.
        // ──────────────────────────────────────────────────────────────
        private void WireSectionReveals()
        {
            try
            {
                HookReveal(OverviewSection);
                HookReveal(LicenseSectionHost);
                HookReveal(ModulesSectionHost);
                HookReveal(DeveloperSectionHost);
                HookReveal(SystemSectionHost);
                HookReveal(DiagnosticsSectionHost);
            }
            catch (System.Exception ex)
            {
                SafeLog("WireSectionReveals", ex);
            }
        }

        // The section's TranslateTransform is intentionally NOT named in XAML
        // (a section is a UserControl with its own sealed NameScope — naming a
        // transform set as its RenderTransform throws MC3093). We reach it here
        // via the FrameworkElement.RenderTransform property instead.
        private static void HookReveal(FrameworkElement? section)
        {
            if (section == null) return;

            section.IsVisibleChanged += (_, args) =>
            {
                try
                {
                    if (args.NewValue is not true) return;

                    var fade = new DoubleAnimation
                    {
                        From = 0,
                        To = 1,
                        Duration = TimeSpan.FromSeconds(0.32),
                    };
                    section.BeginAnimation(OpacityProperty, fade);

                    if (section.RenderTransform is TranslateTransform shift)
                    {
                        var slide = new DoubleAnimation
                        {
                            From = 14,
                            To = 0,
                            Duration = TimeSpan.FromSeconds(0.34),
                            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
                        };
                        shift.BeginAnimation(TranslateTransform.YProperty, slide);
                    }
                }
                catch { /* reveal is decorative — never throw from an event */ }
            };
        }

        private static void SafeLog(string op, System.Exception ex)
        {
            try
            {
                ProGlassAutomation.Services.Logging.AppLogService.Debug(
                    "[AboutView] " + op + " skipped: " + ExceptionMessages.Deep(ex));
            }
            catch { /* logging must never throw */ }
        }

        /// <summary>
        /// Diagnostic surface shown when the About XAML cannot load.
        /// Opaque stage background (never transparent) + the exact error.
        /// </summary>
        private static Border BuildFailurePanel(System.Exception ex)
        {
            var title = new TextBlock
            {
                Text = "About page could not be loaded",
                FontSize = 20,
                FontWeight = FontWeights.Bold,
                Foreground = (System.Windows.Media.Brush)Application.Current.FindResource("OnAccentForegroundBrush"),
                Margin = new Thickness(36, 44, 36, 10),
            };

            var detail = new TextBlock
            {
                Text = ex.GetType().Name + ": " + ExceptionMessages.Deep(ex) +
                       "\n\nThe exact error is shown here and in the app log " +
                       "(Help → Diagnostics). Nothing else in the app is affected.",
                TextWrapping = TextWrapping.Wrap,
                FontSize = 13,
                Foreground = (System.Windows.Media.Brush)Application.Current.FindResource("OnAccentForegroundBrush"),
                Opacity = 0.85,
                Margin = new Thickness(36, 0, 36, 36),
            };

            var panel = new StackPanel();
            panel.Children.Add(title);
            panel.Children.Add(detail);

            var bg = (System.Windows.Media.Brush)Application.Current.FindResource("ModuleBackgroundBrush");

            return new Border
            {
                Background = bg,
                Child = panel,
            };
        }
    }
}
