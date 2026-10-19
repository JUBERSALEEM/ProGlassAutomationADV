// ═══════════════════════════════════════════════════════════════════════
// FILE: Controls/Claymorphism/ClayStatTile.cs
// PROJECT: ProGlassAutomation
// MODULE: Claymorphism — the KPI tile
//
// Three stacked facts — a label, a value, and a delta — moulded as one
// object. This is the reference design system's most-repeated unit, and
// the reason it is its own control rather than three TextBlocks in a
// ClaySurface: the DELTA carries meaning, and meaning must not be left
// to the caller to get right.
//
// ── THE DELTA IS NEVER COLOUR ALONE ──────────────────────────────────
// A green "+12.4%" and a red "-0.4%" are the same shape to anyone who
// cannot separate those hues. The arrow GLYPH is therefore always drawn,
// positive or negative, so the direction survives greyscale, colour
// blindness and a monochrome print. ClayDeltaDirection drives the glyph;
// ClayDeltaBrush only adds emphasis on top of it.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ProGlassAutomation.Services;
using ProGlassAutomation.Services.Theme;
using ProGlassAutomation.Services.Theme.Clay;
using ProGlassAutomation.Services.Theme.SoftUi;

namespace ProGlassAutomation.Controls.Claymorphism
{
    /// <summary>Which way the delta points.</summary>
    public enum ClayDeltaDirection
    {
        /// <summary>Flat — neither up nor down.</summary>
        None = 0,

        /// <summary>Improving.</summary>
        Up = 1,

        /// <summary>Declining.</summary>
        Down = 2,
    }

    /// <summary>A claymorphic stat tile: label, value, delta.</summary>
    public class ClayStatTile : ContentControl
    {
        /// <summary>Tile corner radius. The reference card default is 32.</summary>
        public static readonly DependencyProperty ClayCornerRadiusProperty =
            DependencyProperty.Register(
                nameof(ClayCornerRadius), typeof(CornerRadius),
                typeof(ClayStatTile),
                new FrameworkPropertyMetadata(
                    new CornerRadius(28), OnClayChanged));

        public CornerRadius ClayCornerRadius
        {
            get => (CornerRadius)GetValue(ClayCornerRadiusProperty);
            set => SetValue(ClayCornerRadiusProperty, value);
        }

        /// <summary>Tile colour. Transparent uses the ramp's surface.</summary>
        public static readonly DependencyProperty ClayFillProperty =
            DependencyProperty.Register(
                nameof(ClayFill), typeof(Color),
                typeof(ClayStatTile),
                new FrameworkPropertyMetadata(Colors.Transparent, OnClayChanged));

        public Color ClayFill
        {
            get => (Color)GetValue(ClayFillProperty);
            set => SetValue(ClayFillProperty, value);
        }

        /// <summary>The small caption above the number.</summary>
        public static readonly DependencyProperty ClayLabelTextProperty =
            DependencyProperty.Register(
                nameof(ClayLabelText), typeof(string),
                typeof(ClayStatTile),
                new FrameworkPropertyMetadata(string.Empty, OnClayChanged));

        public string ClayLabelText
        {
            get => (string)GetValue(ClayLabelTextProperty);
            set => SetValue(ClayLabelTextProperty, value);
        }

        /// <summary>The headline number.</summary>
        public static readonly DependencyProperty ClayValueTextProperty =
            DependencyProperty.Register(
                nameof(ClayValueText), typeof(string),
                typeof(ClayStatTile),
                new FrameworkPropertyMetadata(string.Empty, OnClayChanged));

        public string ClayValueText
        {
            get => (string)GetValue(ClayValueTextProperty);
            set => SetValue(ClayValueTextProperty, value);
        }

        /// <summary>The delta text, e.g. "+12.4%".</summary>
        public static readonly DependencyProperty ClayDeltaTextProperty =
            DependencyProperty.Register(
                nameof(ClayDeltaText), typeof(string),
                typeof(ClayStatTile),
                new FrameworkPropertyMetadata(string.Empty, OnClayChanged));

        public string ClayDeltaText
        {
            get => (string)GetValue(ClayDeltaTextProperty);
            set => SetValue(ClayDeltaTextProperty, value);
        }

        /// <summary>
        /// Which way the delta points. Drives the arrow glyph, which is
        /// the cue that makes the direction non-colour.
        /// </summary>
        public static readonly DependencyProperty ClayDeltaDirectionProperty =
            DependencyProperty.Register(
                nameof(ClayDeltaDirection), typeof(ClayDeltaDirection),
                typeof(ClayStatTile),
                new FrameworkPropertyMetadata(ClayDeltaDirection.None, OnClayChanged));

        public ClayDeltaDirection ClayDeltaDirection
        {
            get => (ClayDeltaDirection)GetValue(ClayDeltaDirectionProperty);
            set => SetValue(ClayDeltaDirectionProperty, value);
        }

        /// <summary>Accent used for the delta and the value.</summary>
        public static readonly DependencyProperty ClayAccentProperty =
            DependencyProperty.Register(
                nameof(ClayAccent), typeof(Color),
                typeof(ClayStatTile),
                new FrameworkPropertyMetadata(Colors.Transparent, OnClayChanged));

        public Color ClayAccent
        {
            get => (Color)GetValue(ClayAccentProperty);
            set => SetValue(ClayAccentProperty, value);
        }

        private readonly ClaySurfaceParts _parts = new ClaySurfaceParts();
        private bool _bound;
        private bool _wired;

        public ClayStatTile()
        {
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;
            // A tile is a readout. Making it focusable would put a stop
            // on the tab path for something with nothing to activate.
            Focusable = false;
            Padding = new Thickness(22);
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
            if (d is ClayStatTile t) t.Repaint();
        }

        private void Wire()
        {
            if (_wired) return;
            _wired = true;
            ThemeWeakEventManager.SubscribeThemeChanged((_, _) => Repaint());
        }

        /// <summary>Recomputes and applies the whole recipe.</summary>
        public void Repaint()
        {
            if (!_parts.Complete) return;
            var dark = ThemeManager.CurrentTheme == AppTheme.Dark;
            var state = IsEnabled
                ? SoftInteractionState.Rest
                : SoftInteractionState.Disabled;

            // A stat tile is a data surface: the reference ladder puts
            // data one step above a card, which in the clay tiers means
            // Raised rather than Resting.
            ClaySurfaceParts.Paint(
                _parts, dark, SoftDepthTier.Raised, state,
                ClaySurface.TopLeft(ClayCornerRadius),
                small: false,
                ResolveFill());

            ApplyInk(dark);
            ApplyArrow();
        }

        private Color ResolveFill()
        {
            var baseFill = ClayFill;
            if (baseFill.A == 0)
            {
                var token = Application.Current?.TryFindResource(
                    "SurfacePrimaryBrush") as SolidColorBrush;
                baseFill = token?.Color ?? Colors.White;
            }
            if (!IsEnabled) return ClaySurface.Shade(baseFill, 0.45);
            return baseFill;
        }

        /// <summary>Republishes ink and accent onto the template brushes.</summary>
        private void ApplyInk(bool dark)
        {
            var valueBrush = Application.Current?.TryFindResource(
                "TextPrimaryBrush") as SolidColorBrush;
            var labelBrush = Application.Current?.TryFindResource(
                "TextSecondaryBrush") as SolidColorBrush;

            if (Template?.FindName("ClayTileValueBrush", this)
                is SolidColorBrush vb)
            {
                vb.Color = valueBrush?.Color ?? Colors.Black;
            }
            if (Template?.FindName("ClayTileLabelBrush", this)
                is SolidColorBrush lb)
            {
                lb.Color = labelBrush?.Color ?? Colors.Gray;
            }
            if (Template?.FindName("ClayTileDeltaBrush", this)
                is SolidColorBrush db)
            {
                var accent = ClayAccent;
                if (accent.A == 0)
                {
                    accent = Application.Current?.TryFindResource(
                        "AccentBrush") is SolidColorBrush ab
                        ? ab.Color
                        : Colors.Gray;
                }
                // A declining delta must NOT be tinted with the accent —
                // that would paint bad news in the brand colour. It dims
                // instead, which is also safer for the reader.
                if (ClayDeltaDirection == ClayDeltaDirection.Down)
                {
                    accent = ClaySurface.Shade(accent, 0.35);
                }
                db.Color = accent;
            }
        }

        /// <summary>
        /// Pushes the direction glyph. The arrow is drawn for BOTH
        /// directions — the absence of an arrow would be the only
        /// colour-free difference, and absence is not a cue.
        /// </summary>
        private void ApplyArrow()
        {
            var arrow = Template?.FindName("ClayDeltaArrow", this)
                as TextBlock;
            if (arrow is null) return;

            switch (ClayDeltaDirection)
            {
                case ClayDeltaDirection.Up:
                    arrow.Text = "▲";
                    break;
                case ClayDeltaDirection.Down:
                    arrow.Text = "▼";
                    break;
                default:
                    arrow.Text = "•";
                    break;
            }

            arrow.Visibility = string.IsNullOrWhiteSpace(ClayDeltaText)
                ? Visibility.Collapsed
                : Visibility.Visible;
        }
    }
}
