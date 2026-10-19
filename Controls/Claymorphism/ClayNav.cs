// ═══════════════════════════════════════════════════════════════════════
// FILE: Controls/Claymorphism/ClayNav.cs
// PROJECT: ProGlassAutomation
// MODULE: Claymorphism — the navigation rail
//
// The sidebar: a brand header, a scrolling item list, and an account
// footer. It is a ContentControl with named slots rather than a Control
// with a child collection, because the reference rail has EXACTLY these
// three regions and a generic ItemsControl would let the account footer
// scroll away with the links.
//
// ── THE RAIL IS THE FLATEST CLAY IN THE APP ──────────────────────────
// A tall surface standing beside the page is mostly its own shadow seen
// edge-on. At Resting tier with no hover lift it reads as the wall the
// rows sit on. Promoting it to Raised - which is the obvious choice for
// "it's the most important chrome on the page" - makes it a slab that
// casts a second shadow into the content area.
//
// ── SELECTED ROW ──────────────────────────────────────────────────────
// Selection is ClayMenuRow's job, not the rail's. The rail only decides
// the ItemsHost. That split is what keeps "what does selected look
// like" in one file, which is the same rule the ComboBox follows by
// using ClayMenuRow for its ItemContainerStyle.
using System;
using System.Collections;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ProGlassAutomation.Services;
using ProGlassAutomation.Services.Theme;
using ProGlassAutomation.Services.Theme.Clay;
using ProGlassAutomation.Services.Theme.SoftUi;

namespace ProGlassAutomation.Controls.Claymorphism
{
    /// <summary>A claymorphic navigation rail.</summary>
    public class ClayNav : ContentControl
    {
        /// <summary>Rail corner radius. The rail is square on two sides.</summary>
        public static readonly DependencyProperty ClayCornerRadiusProperty =
            DependencyProperty.Register(
                nameof(ClayCornerRadius), typeof(CornerRadius),
                typeof(ClayNav),
                new FrameworkPropertyMetadata(
                    new CornerRadius(24), OnClayChanged));

        public CornerRadius ClayCornerRadius
        {
            get => (CornerRadius)GetValue(ClayCornerRadiusProperty);
            set => SetValue(ClayCornerRadiusProperty, value);
        }

        /// <summary>Rail colour. Transparent uses the ramp's chrome.</summary>
        public static readonly DependencyProperty ClayFillProperty =
            DependencyProperty.Register(
                nameof(ClayFill), typeof(Color),
                typeof(ClayNav),
                new FrameworkPropertyMetadata(Colors.Transparent, OnClayChanged));

        public Color ClayFill
        {
            get => (Color)GetValue(ClayFillProperty);
            set => SetValue(ClayFillProperty, value);
        }

        /// <summary>Brand mark and product name.</summary>
        public static readonly DependencyProperty ClayBrandProperty =
            DependencyProperty.Register(
                nameof(ClayBrand), typeof(object),
                typeof(ClayNav),
                new FrameworkPropertyMetadata(null, OnClayChanged));

        public object? ClayBrand
        {
            get => GetValue(ClayBrandProperty);
            set => SetValue(ClayBrandProperty, value);
        }

        /// <summary>Account block. Pinned to the bottom.</summary>
        public static readonly DependencyProperty ClayAccountProperty =
            DependencyProperty.Register(
                nameof(ClayAccount), typeof(object),
                typeof(ClayNav),
                new FrameworkPropertyMetadata(null, OnClayChanged));

        public object? ClayAccount
        {
            get => GetValue(ClayAccountProperty);
            set => SetValue(ClayAccountProperty, value);
        }

        /// <summary>The navigation items. Each is a ClayMenuRow.</summary>
        public static readonly DependencyProperty ClayItemsProperty =
            DependencyProperty.Register(
                nameof(ClayItems), typeof(IEnumerable),
                typeof(ClayNav),
                new FrameworkPropertyMetadata(null, OnClayChanged));

        public IEnumerable? ClayItems
        {
            get => (IEnumerable?)GetValue(ClayItemsProperty);
            set => SetValue(ClayItemsProperty, value);
        }

        private readonly ClaySurfaceParts _parts = new ClaySurfaceParts();
        private bool _bound;
        private bool _wired;

        public ClayNav()
        {
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;
            // The rail itself is chrome, not a tab stop. Its rows are.
            Focusable = false;
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
            if (d is ClayNav n) n.Repaint();
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

            // Resting, and never promoted. See the header note.
            ClaySurfaceParts.Paint(
                _parts, dark, SoftDepthTier.Resting, SoftInteractionState.Rest,
                ClaySurface.TopLeft(ClayCornerRadius),
                small: false,
                ResolveFill(dark));
        }

        private Color ResolveFill(bool dark)
        {
            var baseFill = ClayFill;
            if (baseFill.A == 0)
            {
                var token = Application.Current?.TryFindResource(
                    "ChromeBrush") as SolidColorBrush;
                baseFill = token?.Color ?? Colors.White;
            }
            return IsEnabled ? baseFill : ClaySurface.Shade(baseFill, 0.45);
        }
    }
}
