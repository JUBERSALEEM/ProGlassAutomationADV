// ═══════════════════════════════════════════════════════════════════════
// FILE: Controls/Aurora/AuroraCard.cs
// PROJECT: ProGlassAutomation
// MODULE: Aurora — the KPI card
//
// ── NOT FOCUSABLE, ON PURPOSE ─────────────────────────────────────────
//
// This is the control that most tempts a house style into making every
// panel tabbable, and every attempt is wrong for a measurable reason: a
// dashboard of twelve KPI cards then presents twelve dead stops in the
// tab order, and a keyboard user spends the whole page reaching cards
// that do nothing when they land.
//
// So AuroraCard is NOT focusable by default. If a card ever becomes
// interactive it should BE an AuroraButton, which carries the focus ring
// and the dip already -- not a card that grew a tab stop and no gesture.
//
// Hover still moves the surface, because hover is pointer-only and costs
// nothing in the tab order. What hover must not do is promote the card to
// a different tier: Aurora has no tiers, and borrowing Clay's "hover
// lifts one tier" here would import a concept this family does not have.
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ProGlassAutomation.Services;
using ProGlassAutomation.Services.Theme;
using ProGlassAutomation.Services.Theme.Aurora;
using ProGlassAutomation.Services.Theme.SoftUi;

namespace ProGlassAutomation.Controls.Aurora
{
    /// <summary>A translucent panel in the sky. Non-interactive.</summary>
    public class AuroraCard : ContentControl
    {
        public static readonly DependencyProperty AuroraDepthProperty =
            DependencyProperty.Register(
                nameof(AuroraDepth), typeof(SoftDepthTier),
                typeof(AuroraCard),
                new FrameworkPropertyMetadata(
                    SoftDepthTier.Resting,
                    FrameworkPropertyMetadataOptions.AffectsRender,
                    OnAuroraCardChanged));

        public SoftDepthTier AuroraDepth
        {
            get => (SoftDepthTier)GetValue(AuroraDepthProperty);
            set => SetValue(AuroraDepthProperty, value);
        }

        private static void OnAuroraCardChanged(
            DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is AuroraCard c) c.Repaint();
        }

        private readonly AuroraSurfaceParts _parts = new AuroraSurfaceParts();
        private bool _bound;
        private bool _wired;

        public AuroraCard()
        {
            // See the file header. Not focusable is a decision, not an
            // omission.
            Focusable = false;
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            if (!_bound)
            {
                _parts.Bind(this);
                _bound = true;
                Wire();
            }

            Repaint();
        }

        private void Wire()
        {
            if (_wired) return;
            _wired = true;

            ThemeWeakEventManager.SubscribeThemeChanged((_, _) => Repaint());
            AuroraPalette.Changed += (_, _) => Repaint();

            DependencyPropertyDescriptor.FromProperty(
                IsMouseOverProperty, typeof(Control))
                .AddValueChanged(this, (_, _) => Repaint());
        }

        public void Repaint()
        {
            if (!_parts.Complete) return;

            var dark = ThemeManager.CurrentTheme == AppTheme.Dark;

            AuroraSurfaceParts.Paint(
                _parts, dark,
                AuroraPlacement.Panel,
                AuroraDepth,
                ResolveState(),
                interactive: true,
                selected: false,
                AuroraSurface.FallbackFill(),
                AuroraSurface.FallbackInk());
        }

        private SoftInteractionState ResolveState()
        {
            if (!IsEnabled) return SoftInteractionState.Disabled;
            if (IsMouseOver) return SoftInteractionState.Hover;
            return SoftInteractionState.Rest;
        }
    }
}
