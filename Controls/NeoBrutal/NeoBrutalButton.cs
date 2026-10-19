// ═══════════════════════════════════════════════════════════════════════
// FILE: Controls/NeoBrutal/NeoBrutalButton.cs
// PROJECT: ProGlassAutomation
// MODULE: Neo-Brutalism — the stamped action
//
// ── PRESS IS A STAMP, AND IT IS APPLIED INSTANTLY ────────────────────
//
// There is no animation anywhere in this control, and that is a design
// decision rather than an omission. "Do not let a default transition near
// the press. The gesture is impact, and impact has no curve."
//
// Every other family in this engine runs a spring or an ease on press.
// A 120ms ease here would turn a stamp into a squash -- the block would
// arrive rather than strike. NeoBrutalMetrics.PressDuration is 0 and the
// transforms are assigned directly by NeoBrutalSurfaceParts.Apply.
//
// ── THE PRIMARY ACTION IS AN ACID FILL WITH INK ON IT ─────────────────
//
// The accent label is black, not white, and that is enforced by
// NeoBrutalPalette.EnsureAccentInk rather than left to the template --
// see that method for the measured numbers. Black on the shipped acid
// yellow is 14.80:1; white is 1.34:1.
//
// ── REDUCED MOTION KILLS THE MOVE, NOT THE STATE ─────────────────────
//
// With motion off the block does not travel, but the border still
// thickens, because the border weight is the half of the press signal
// that is not animation. Removing both would leave a pressed button
// distinguishable only by a 1px border change, which is not enough.
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using ProGlassAutomation.Services;
using System.Windows.Media;
using ProGlassAutomation.Services.Theme;
using ProGlassAutomation.Services.Theme.NeoBrutal;
using ProGlassAutomation.Services.Theme.SoftUi;

namespace ProGlassAutomation.Controls.NeoBrutal
{
    /// <summary>A block that can be activated.</summary>
    public class NeoBrutalButton : ButtonBase
    {
        public static readonly DependencyProperty NeoPlacementProperty =
            DependencyProperty.Register(
                nameof(NeoPlacement), typeof(NeoBrutalPlacement),
                typeof(NeoBrutalButton),
                new FrameworkPropertyMetadata(
                    NeoBrutalPlacement.Control,
                    FrameworkPropertyMetadataOptions.AffectsRender,
                    OnNeoChanged),
                OnPlacementCoerce);

        public static readonly DependencyProperty NeoDepthProperty =
            DependencyProperty.Register(
                nameof(NeoDepth), typeof(SoftDepthTier),
                typeof(NeoBrutalButton),
                new FrameworkPropertyMetadata(
                    SoftDepthTier.Raised,
                    FrameworkPropertyMetadataOptions.AffectsRender,
                    OnNeoChanged));

        public static readonly DependencyProperty NeoStateProperty =
            DependencyProperty.Register(
                nameof(NeoState), typeof(SoftInteractionState),
                typeof(NeoBrutalButton),
                new FrameworkPropertyMetadata(
                    SoftInteractionState.Rest,
                    FrameworkPropertyMetadataOptions.AffectsRender,
                    OnNeoChanged));

        public static readonly DependencyProperty NeoSelectedProperty =
            DependencyProperty.Register(
                nameof(NeoSelected), typeof(bool),
                typeof(NeoBrutalButton),
                new FrameworkPropertyMetadata(
                    false,
                    FrameworkPropertyMetadataOptions.AffectsRender,
                    OnNeoChanged));

        /// <summary>
        /// Marks the primary action. This is what puts the acid fill on
        /// the block; there is no gradient and no glow anywhere in this
        /// family, so the accent fill IS the emphasis.
        /// </summary>
        public static readonly DependencyProperty NeoPrimaryProperty =
            DependencyProperty.Register(
                nameof(NeoPrimary), typeof(bool),
                typeof(NeoBrutalButton),
                new FrameworkPropertyMetadata(
                    false,
                    FrameworkPropertyMetadataOptions.AffectsRender,
                    OnNeoChanged));

        /// <summary>Stamps the block in the danger accent.</summary>
        public static readonly DependencyProperty NeoDangerProperty =
            DependencyProperty.Register(
                nameof(NeoDanger), typeof(bool),
                typeof(NeoBrutalButton),
                new FrameworkPropertyMetadata(
                    false,
                    FrameworkPropertyMetadataOptions.AffectsRender,
                    OnNeoChanged));

        public NeoBrutalPlacement NeoPlacement
        {
            get => (NeoBrutalPlacement)GetValue(NeoPlacementProperty);
            set => SetValue(NeoPlacementProperty, value);
        }

        public SoftDepthTier NeoDepth
        {
            get => (SoftDepthTier)GetValue(NeoDepthProperty);
            set => SetValue(NeoDepthProperty, value);
        }

        public SoftInteractionState NeoState
        {
            get => (SoftInteractionState)GetValue(NeoStateProperty);
            set => SetValue(NeoStateProperty, value);
        }

        public bool NeoSelected
        {
            get => (bool)GetValue(NeoSelectedProperty);
            set => SetValue(NeoSelectedProperty, value);
        }

        public bool NeoPrimary
        {
            get => (bool)GetValue(NeoPrimaryProperty);
            set => SetValue(NeoPrimaryProperty, value);
        }

        public bool NeoDanger
        {
            get => (bool)GetValue(NeoDangerProperty);
            set => SetValue(NeoDangerProperty, value);
        }

        /// <summary>
        /// A Page placement would draw the block with no border and no
        /// offset -- i.e. nothing at all, which is not a button.
        /// </summary>
        private static object OnPlacementCoerce(
            DependencyObject d, object baseValue)
        {
            return (NeoBrutalPlacement)baseValue == NeoBrutalPlacement.Page
                ? NeoBrutalPlacement.Control
                : baseValue;
        }

        private static void OnNeoChanged(
            DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is NeoBrutalButton b) b.Repaint();
        }

        private readonly NeoBrutalSurfaceParts _parts = new NeoBrutalSurfaceParts();
        private bool _bound;
        private bool _wired;

        public NeoBrutalButton()
        {
            HorizontalContentAlignment = HorizontalAlignment.Center;
            VerticalContentAlignment = VerticalAlignment.Center;
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;

            // The focus cue is drawn by PART_NeoFocus, outside the border.
            // See NeoBrutalSurfaceParts for why the default ring is eaten.
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
                IsPressedProperty, typeof(ButtonBase))
                .AddValueChanged(this, (_, _) => Repaint());

            DependencyPropertyDescriptor.FromProperty(
                IsKeyboardFocusedProperty, typeof(KeyboardNavigation))
                .AddValueChanged(this, (_, _) => Repaint());

            DependencyPropertyDescriptor.FromProperty(
                IsMouseOverProperty, typeof(ButtonBase))
                .AddValueChanged(this, (_, _) => Repaint());
        }

        public void Repaint()
        {
            if (!_parts.Complete) return;

            var dark = IsDark();
            var state = ResolveState();
            var selected = NeoSelected || NeoPrimary || NeoDanger;

            NeoBrutalSurfaceParts.Paint(
                _parts, dark, NeoPlacement, NeoDepth, state,
                interactive: true, selected: selected,
                NeoBrutalButton.FallbackFill(),
                NeoBrutalButton.FallbackInk());

            // The accent fill, applied after the recipe rather than in it.
            //
            // Primary and Danger are properties of the control's ROLE,
            // not of its state: a primary button is primary at rest too.
            // The recipe resolves state, so the accent is applied here --
            // doing it in XAML instead would mean a Style setter
            // fighting Apply() on every repaint, and the winner would
            // depend on template-apply order.
            ApplyAccent(dark, state);
        }

        private void ApplyAccent(bool dark, SoftInteractionState state)
        {
            if (_parts.Fill is null) return;
            if (state == SoftInteractionState.Disabled) return;
            if (!NeoPrimary && !NeoDanger) return;

            var profile = NeoBrutalPalette.For(dark);
            if (profile is null) return;

            Color fill = NeoDanger ? profile.Danger : profile.Accent;
            Color ink = profile.AccentForeground;

            if (state == SoftInteractionState.Pressed)
            {
                // Pressed blocks sit IN their own shadow, so the fill has
                // to lose contrast against the plate it is landing on.
                fill = NeoBrutalRecipe.Mix(fill, profile.Shadow, 0.18);
            }
            else if (state == SoftInteractionState.Hover)
            {
                fill = NeoBrutalRecipe.Mix(fill, Colors.White, 0.06);
            }

            var brush = new SolidColorBrush(fill);
            brush.Freeze();
            _parts.Fill.Background = brush;

            var label = new SolidColorBrush(ink);
            label.Freeze();
            Foreground = label;
        }

        private SoftInteractionState ResolveState()
        {
            if (!IsEnabled) return SoftInteractionState.Disabled;

            if (NeoState == SoftInteractionState.Pressed || IsPressed)
                return SoftInteractionState.Pressed;
            if (NeoState == SoftInteractionState.Focused
                || IsKeyboardFocused)
                return SoftInteractionState.Focused;
            if (NeoState == SoftInteractionState.Hover || IsMouseOver)
                return SoftInteractionState.Hover;

            return SoftInteractionState.Rest;
        }

        private static bool IsDark()
        {
            try { return ThemeManager.CurrentTheme == AppTheme.Dark; }
            catch (Exception) { return false; }
        }

        /// <summary>Fallback fill outside a Neo-Brutalist theme.</summary>
        public static Color FallbackFill() =>
            Color.FromArgb(0xFF, 0xFF, 0xFD, 0xF5);

        /// <summary>Fallback ink outside a Neo-Brutalist theme.</summary>
        public static Color FallbackInk() =>
            Color.FromArgb(0xFF, 0x0A, 0x0A, 0x0A);

        /// <summary>
        /// WCAG 2.2 SC 2.5.8. The 4px border and 5px offset make a block
        /// LOOK large, but neither adds a pixel of hit area -- which is
        /// the documented trap ("visual bulk from borders is not the
        /// actual clickable area").
        /// </summary>
        protected override Size MeasureOverride(Size availableSize)
        {
            var size = base.MeasureOverride(availableSize);

            double floor = SoftUiAccessibility.MinHitTarget;
            if (size.Height < floor) size.Height = floor;
            if (size.Width < floor) size.Width = floor;

            return size;
        }
    }
}
