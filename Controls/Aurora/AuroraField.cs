// ═══════════════════════════════════════════════════════════════════════
// FILE: Controls/Aurora/AuroraField.cs
// PROJECT: ProGlassAutomation
// MODULE: Aurora — text entry, and the one place the mesh almost wins
//
// ── THIS IS THE HARDEST SURFACE IN THE FAMILY ────────────────────────
//
// Aurora's whole premise is a moving backdrop, and this is the control
// where that premise is most dangerous: the user types into it, character
// by character, with a caret that must stay findable.
//
// So the field defaults to Content placement, not Panel. That is not a
// styling preference -- ContentPlacement is the branch in AuroraRecipe
// that composites to AuroraMetrics.ContentOpacity before the ink is ever
// resolved. A text field at Panel opacity would be readable in a
// screenshot and unusable in the hand, because the blob drifting behind
// it changes the contrast on every pass.
//
// The rim is what the user actually tracks, and it is why the rim is
// drawn at full strength in every state including rest.
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
    /// <summary>A text field that stays readable over a moving sky.</summary>
    public class AuroraField : TextBox
    {
        public static readonly DependencyProperty AuroraDepthProperty =
            DependencyProperty.Register(
                nameof(AuroraDepth), typeof(SoftDepthTier),
                typeof(AuroraField),
                new FrameworkPropertyMetadata(
                    SoftDepthTier.Resting,
                    FrameworkPropertyMetadataOptions.AffectsRender,
                    OnAuroraFieldChanged));

        public SoftDepthTier AuroraDepth
        {
            get => (SoftDepthTier)GetValue(AuroraDepthProperty);
            set => SetValue(AuroraDepthProperty, value);
        }

        private static void OnAuroraFieldChanged(
            DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is AuroraField f) f.Repaint();
        }

        private readonly AuroraSurfaceParts _parts = new AuroraSurfaceParts();
        private bool _bound;
        private bool _wired;

        public AuroraField()
        {
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;
            FocusVisualStyle = null;
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
                IsKeyboardFocusedProperty, typeof(KeyboardNavigation))
                .AddValueChanged(this, (_, _) => Repaint());

            DependencyPropertyDescriptor.FromProperty(
                IsMouseOverProperty, typeof(Control))
                .AddValueChanged(this, (_, _) => Repaint());

            DependencyPropertyDescriptor.FromProperty(
                IsEnabledProperty, typeof(Control))
                .AddValueChanged(this, (_, _) => Repaint());
        }

        public void Repaint()
        {
            if (!_parts.Complete) return;

            var dark = ThemeManager.CurrentTheme == AppTheme.Dark;

            // Content, unconditionally. See the file header.
            AuroraSurfaceParts.Paint(
                _parts, dark,
                AuroraPlacement.Content,
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
            if (IsKeyboardFocused) return SoftInteractionState.Focused;
            if (IsMouseOver) return SoftInteractionState.Hover;
            return SoftInteractionState.Rest;
        }
    }
}
