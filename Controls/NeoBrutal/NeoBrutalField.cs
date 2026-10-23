// ═══════════════════════════════════════════════════════════════════════
// FILE: Controls/NeoBrutal/NeoBrutalField.cs
// PROJECT: ProGlassAutomation
// MODULE: Neo-Brutalism — text entry
//
// ── THE BORDER IS THE FOCUS CUE HERE, SO IT IS NOT ALLOWED TO BE BOTH ─
//
// Every other family draws a separate focus ring because its border is
// quiet enough to survive one being drawn over it. Here the border is
// already 4px and already the most assertive thing on the control, so
// "focus" has to change the block's own weight rather than add an
// outline that the border would swallow.
//
// Which is why ResolveState still reports Focused, and the recipe
// thickens to BorderThick on focus: the cue is a WEIGHT change on the
// element the user is already looking at.
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ProGlassAutomation.Services;
using ProGlassAutomation.Services.Theme;
using ProGlassAutomation.Services.Theme.NeoBrutal;
using ProGlassAutomation.Services.Theme.SoftUi;

namespace ProGlassAutomation.Controls.NeoBrutal
{
    /// <summary>A stamped text field.</summary>
    public class NeoBrutalField : TextBox
    {
        public static readonly DependencyProperty NeoDepthProperty =
            DependencyProperty.Register(
                nameof(NeoDepth), typeof(SoftDepthTier),
                typeof(NeoBrutalField),
                new FrameworkPropertyMetadata(
                    SoftDepthTier.Resting,
                    FrameworkPropertyMetadataOptions.AffectsRender,
                    OnNeoFieldChanged));

        public SoftDepthTier NeoDepth
        {
            get => (SoftDepthTier)GetValue(NeoDepthProperty);
            set => SetValue(NeoDepthProperty, value);
        }

        private static void OnNeoFieldChanged(
            DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is NeoBrutalField f) f.Repaint();
        }

        private readonly NeoBrutalSurfaceParts _parts = new NeoBrutalSurfaceParts();
        private bool _bound;
        private bool _wired;

        public NeoBrutalField()
        {
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;

            // PART_NeoFocus draws the outline OUTSIDE the 4px border,
            // which is the only way a focus ring survives here.
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
            NeoBrutalPalette.Changed += (_, _) => Repaint();

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

            bool dark;
            try { dark = ThemeManager.CurrentTheme == AppTheme.Dark; }
            catch (Exception) { dark = false; }

            NeoBrutalSurfaceParts.Paint(
                _parts, dark,
                NeoBrutalPlacement.Control,
                NeoDepth, ResolveState(),
                interactive: true,
                selected: false,
                NeoBrutalButton.FallbackFill(),
                NeoBrutalButton.FallbackInk());
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
