// ═══════════════════════════════════════════════════════════════════════
// FILE: Controls/Claymorphism/ClayWell.cs
// PROJECT: ProGlassAutomation
// MODULE: Claymorphism — the recessed input
//
// ── THE ONE PLACE CLAY INVERTS ───────────────────────────────────────
// Everything else in this style lifts. A field you are about to TYPE
// INTO must do the opposite: it must recede. A lifted input tells the
// hand "press me", and the first thing pressed is a text field that
// does nothing at all.
//
// So a well has NO outer drop. It has the deepest inset pair of any
// tier — the far wall is thicker, not the near one — and its fill sits
// BELOW the page rather than above it. This is the single detail that
// separates a clay form from a keyboard made of pillows, and it is the
// one every shallow copy of this style gets wrong.
//
// It is a ContentControl rather than a TextBox subclass so it can wrap
// ANY input — a search box, a combobox's editable box, a date field —
// with one well. The hosted control supplies a transparent background
// and inherits the well's text colour; the well supplies the form.
// ═══════════════════════════════════════════════════════════════════════

using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ProGlassAutomation.Services.Theme;
using ProGlassAutomation.Services.Theme.Clay;
using ProGlassAutomation.Services.Theme.SoftUi;

namespace ProGlassAutomation.Controls.Claymorphism
{
    /// <summary>A recessed clay surface for text entry.</summary>
    public class ClayWell : ContentControl
    {
        #region ── Dependency properties ──

        /// <summary>
        /// Corner radius. Wells are usually a little tighter than cards —
        /// an input is a smaller object, and the radius/inset ratio has
        /// to keep the roll resolvable.
        /// </summary>
        public static readonly DependencyProperty ClayCornerRadiusProperty =
            DependencyProperty.Register(
                nameof(ClayCornerRadius), typeof(CornerRadius),
                typeof(ClayWell),
                new FrameworkPropertyMetadata(
                    new CornerRadius(16), OnClayChanged));

        public CornerRadius ClayCornerRadius
        {
            get => (CornerRadius)GetValue(ClayCornerRadiusProperty);
            set => SetValue(ClayCornerRadiusProperty, value);
        }

        /// <summary>
        /// The well's own colour. It should read as a recess, so this is
        /// normally slightly DARKER than the surface it sits in — the
        /// opposite of every other clay surface in the app.
        /// </summary>
        public static readonly DependencyProperty ClayFillProperty =
            DependencyProperty.Register(
                nameof(ClayFill), typeof(Color),
                typeof(ClayWell),
                new FrameworkPropertyMetadata(Colors.Transparent, OnClayChanged));

        public Color ClayFill
        {
            get => (Color)GetValue(ClayFillProperty);
            set => SetValue(ClayFillProperty, value);
        }

        /// <summary>
        /// The focus ring width. A focused well needs a ring more than
        /// any other control — an inset highlight in a recess is very
        /// close to invisible, which is precisely the trap.
        /// </summary>
        public static readonly DependencyProperty ClayFocusRingWidthProperty =
            DependencyProperty.Register(
                nameof(ClayFocusRingWidth), typeof(double),
                typeof(ClayWell),
                new FrameworkPropertyMetadata(3.0, OnClayChanged));

        public double ClayFocusRingWidth
        {
            get => (double)GetValue(ClayFocusRingWidthProperty);
            set => SetValue(ClayFocusRingWidthProperty, value);
        }

        #endregion

        private readonly ClaySurfaceParts _parts = new ClaySurfaceParts();
        private bool _bound;
        private bool _wired;

        public ClayWell()
        {
            HorizontalContentAlignment = HorizontalAlignment.Stretch;
            VerticalContentAlignment = VerticalAlignment.Center;
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

        private static void OnClayChanged(
            DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ClayWell w) w.Repaint();
        }

        private void Wire()
        {
            if (_wired) return;
            _wired = true;
            ThemeWeakEventManager.SubscribeThemeChanged((_, _) => Repaint());
            DependencyPropertyDescriptor.FromProperty(
                IsKeyboardFocusWithinProperty,
                typeof(Control))
                .AddValueChanged(this, (_, _) => Repaint());

            // The ring width is a property, so the template binds it.
            // Until now it was declared and never read — which is the
            // usual way a "configurable" focus ring quietly stays at
            // whatever the default happened to be.
        }

        /// <summary>Recomputes and applies the well.</summary>
        public void Repaint()
        {
            if (!_parts.Complete) return;

            var dark = ThemeManager.CurrentTheme == AppTheme.Dark;

            ClaySurfaceParts.Paint(
                _parts, ClayPalette, dark,
                SoftDepthTier.Well,
                ResolveState(),
                ClaySurface.TopLeft(ClayCornerRadius),
                smallSurface: false,
                ResolveFill(dark));
        }

        private SoftInteractionState ResolveState()
        {
            if (!IsEnabled) return SoftInteractionState.Disabled;
            return IsKeyboardFocusWithin
                ? SoftInteractionState.Focused
                : SoftInteractionState.Rest;
        }

        private Color ResolveFill(bool dark)
        {
            if (ClayFill.A > 0) return ClayFill;

            // A well sits BELOW the page, so it takes the module colour
            // rather than the surface colour. Using Surface here is the
            // bug that makes a well read as a raised card with a border.
            var brush = Application.Current?.TryFindResource(
                "ModuleBackgroundBrush") as SolidColorBrush;
            return brush?.Color ?? Colors.Gray;
        }
    }
}