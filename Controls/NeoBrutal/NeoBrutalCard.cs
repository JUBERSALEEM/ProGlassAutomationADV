// ═══════════════════════════════════════════════════════════════════════
// FILE: Controls/NeoBrutal/NeoBrutalCard.cs
// PROJECT: ProGlassAutomation
// MODULE: Neo-Brutalism — the data block
//
// ── NOT FOCUSABLE, AND THE REASON IS SHARPER HERE ────────────────────
//
// Twelve KPI blocks that each present a dead tab stop is the same problem
// it is in every other family, but this one compounds it: a 4px border
// and a 5px hard offset make each block LOOK like a button. A sighted
// user will try to click them. So the blocks must not merely avoid
// pretending -- they must actually not be interactive, and the accent
// variants (NeoBrutalCard.NeoAccent) are the sanctioned way to mark the
// ones that ARE.
//
// If a card ever becomes interactive it should BE a NeoBrutalButton,
// which already carries the stamp, the focus outline and the press.
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ProGlassAutomation.Services;
using System.Windows.Media;
using ProGlassAutomation.Services.Theme;
using ProGlassAutomation.Services.Theme.NeoBrutal;
using ProGlassAutomation.Services.Theme.SoftUi;

namespace ProGlassAutomation.Controls.NeoBrutal
{
    /// <summary>A stamped, non-interactive block.</summary>
    public class NeoBrutalCard : ContentControl
    {
        public static readonly DependencyProperty NeoDepthProperty =
            DependencyProperty.Register(
                nameof(NeoDepth), typeof(SoftDepthTier),
                typeof(NeoBrutalCard),
                new FrameworkPropertyMetadata(
                    SoftDepthTier.Resting,
                    FrameworkPropertyMetadataOptions.AffectsRender,
                    OnNeoCardChanged));

        /// <summary>
        /// Stamps the block in the accent fill. This is how a card
        /// declares that it carries a headline figure, without making it
        /// look clickable.
        /// </summary>
        public static readonly DependencyProperty NeoAccentProperty =
            DependencyProperty.Register(
                nameof(NeoAccent), typeof(bool),
                typeof(NeoBrutalCard),
                new FrameworkPropertyMetadata(
                    false,
                    FrameworkPropertyMetadataOptions.AffectsRender,
                    OnNeoCardChanged));

        public SoftDepthTier NeoDepth
        {
            get => (SoftDepthTier)GetValue(NeoDepthProperty);
            set => SetValue(NeoDepthProperty, value);
        }

        public bool NeoAccent
        {
            get => (bool)GetValue(NeoAccentProperty);
            set => SetValue(NeoAccentProperty, value);
        }

        private static void OnNeoCardChanged(
            DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is NeoBrutalCard c) c.Repaint();
        }

        private readonly NeoBrutalSurfaceParts _parts = new NeoBrutalSurfaceParts();
        private bool _bound;
        private bool _wired;

        public NeoBrutalCard()
        {
            // A decision, not an omission -- see the file header.
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
            NeoBrutalPalette.Changed += (_, _) => Repaint();

            DependencyPropertyDescriptor.FromProperty(
                IsMouseOverProperty, typeof(Control))
                .AddValueChanged(this, (_, _) => Repaint());
        }

        public void Repaint()
        {
            if (!_parts.Complete) return;

            bool dark;
            try { dark = ThemeManager.CurrentTheme == AppTheme.Dark; }
            catch (Exception) { dark = false; }

            var state = ResolveState();

            NeoBrutalSurfaceParts.Paint(
                _parts, dark,
                NeoBrutalPlacement.Content,
                NeoDepth, state,
                interactive: true,
                selected: NeoAccent,
                NeoBrutalButton.FallbackFill(),
                NeoBrutalButton.FallbackInk());

            // Accent blocks carry ink text, never white.
            if (NeoAccent && state != SoftInteractionState.Disabled)
            {
                var profile = NeoBrutalPalette.For(dark);
                if (profile is not null && _parts.Fill is not null)
                {
                    var brush = new SolidColorBrush(profile.Accent);
                    brush.Freeze();
                    _parts.Fill.Background = brush;

                    var label = new SolidColorBrush(
                        profile.AccentForeground);
                    label.Freeze();
                    Foreground = label;
                }
            }
        }

        private SoftInteractionState ResolveState()
        {
            if (!IsEnabled) return SoftInteractionState.Disabled;
            if (IsMouseOver) return SoftInteractionState.Hover;
            return SoftInteractionState.Rest;
        }
    }
}
