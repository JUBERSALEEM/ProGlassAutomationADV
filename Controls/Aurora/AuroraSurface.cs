// ═══════════════════════════════════════════════════════════════════════
// FILE: Controls/Aurora/AuroraSurface.cs
// PROJECT: ProGlassAutomation
// MODULE: Aurora — the sky, as a control
//
// ── THIS IS THE ONLY PART OF AURORA THAT IS NOT A SURFACE ─────────────
//
// Every other family builds materials that sit ON something. Aurora also
// owns the thing they sit on, because the backdrop is not decoration
// here -- it is the family's premise, and a theme engine that cannot
// produce its own backdrop cannot produce this family at all.
//
// Wrap the app root in an AuroraSurface and the sky exists. Everything
// else in this family is described relative to it.
//
// ── WHY THE BLOBS ARE ELEMENT-LEVEL, NOT BRUSH-LEVEL ─────────────────
//
// The obvious way to draw an aurora in WPF is one brush with four gradient
// stops and an animated Offset. That is also the version that costs 60fps
// to render, because moving a GradientStop invalidates the brush and the
// whole backdrop is re-rasterised every single frame.
//
// So each blob is its own Ellipse carrying its own FROZEN RadialGradientBrush,
// and the drift animates only the Ellipse's RenderTransform. A transform on
// an element with no effect and a frozen brush is composited by the GPU.
// Three frozen ellipses cost one rasterisation, at startup, forever.
//
// This is the WPF expression of the rule the web sources state as
// "transform-only keyframes, never background-position -- that repaints
// the gradient every frame".
//
// ── REDUCED MOTION IS A COMPOSED STILL, NOT A BLANK ONE ───────────────
//
// Drift() returns null when motion is off, and each blob is then laid out
// at its authored resting position -- which is the same composition the
// animation starts from. The user sees a designed image, not a missing
// feature. Every other family degrades toward "off"; this one degrades
// toward "poster".
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using ProGlassAutomation.Services;
using ProGlassAutomation.Services.Theme;
using ProGlassAutomation.Services.Theme.Aurora;
using ProGlassAutomation.Services.Theme.Eight;

namespace ProGlassAutomation.Controls.Aurora
{
    /// <summary>
    /// The Aurora sky. Hosts three drifting blobs over a deep wash.
    /// </summary>
    public class AuroraSurface : ContentControl
    {
        /// <summary>Placement is fixed: this IS the backdrop.</summary>
        public const AuroraPlacement Placement = AuroraPlacement.Backdrop;

        private readonly List<Ellipse> _blobs = new List<Ellipse>();
        private Canvas? _host;
        private Border? _wash;

        public AuroraSurface()
        {
            // The sky is not a hit-test target -- but that is a property
            // of the BLOBS, not of this control.
            //
            // This control WRAPS the app root: it is a ContentControl
            // whose template hosts a ContentPresenter. Setting
            // IsHitTestVisible = False here disables hit-testing for the
            // ENTIRE subtree, so every button, field and row inside the
            // window stops receiving mouse input. It looks like a working
            // app and does nothing at all when clicked.
            //
            // The intent was only ever to stop three decorative Ellipses
            // intercepting pointers, and each Ellipse already sets it
            // individually. Background stays null so the wash itself is
            // not a target either -- a null-background Border is not
            // hit-testable in the first place.
            Background = null;
            Focusable = false;
        }

        /// <summary>
        /// Fallback fill for Aurora controls rendering under a
        /// non-Aurora theme. Near-white on purpose: an Aurora control
        /// that cannot resolve a profile must still be a READABLE pane,
        /// not an invisible one.
        /// </summary>
        public static Color FallbackFill() =>
            Color.FromArgb(0xFF, 0x14, 0x12, 0x1F);

        /// <summary>Fallback ink for the same case.</summary>
        public static Color FallbackInk() => Colors.White;

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            _wash = Template.FindName("PART_AuroraFill", this) as Border;
            _host = Template.FindName("PART_AuroraBlobHost", this) as Canvas;

            Build();

            // The sky has to follow the app between light and dark. It is
            // not a decoration that can be painted once: both Aurora
            // ramps are dark, but they are DIFFERENT darks, and holding
            // the light ramp while the rest of the window sits in dark
            // leaves the one surface nobody else is responsible for
            // visibly out of step with everything around it.
            ThemeWeakEventManager.SubscribeThemeChanged((_, _) => Build());
        }

        /// <summary>
        /// Rebuilds the sky. Safe to call repeatedly -- it clears and
        /// re-lays every blob rather than stacking duplicates.
        /// </summary>
        public void Build()
        {
            if (_host is null) return;

            var profile = AuroraPalette.For(IsDark());
            if (profile is null)
            {
                // No Aurora profile published: leave the wash alone and
                // drop the sky, so this control degrades to a plain
                // background instead of painting a black rectangle.
                _host.Children.Clear();
                _blobs.Clear();
                if (_wash is not null) _wash.Background = null;
                return;
            }

            if (_wash is not null)
            {
                var wash = AuroraMesh.BaseWash(profile, false);
                _wash.Background = wash;
            }

            _host.Children.Clear();
            _blobs.Clear();

            bool motion = SafeMotionAllowed();

            var specs = AuroraMesh.Blobs;
            var metrics = profile.Metrics;

            for (int i = 0; i < specs.Count && i < metrics.BlobCount; i++)
            {
                var spec = specs[i];

                var colour = i switch
                {
                    0 => profile.BlobIndigo,
                    1 => profile.BlobViolet,
                    _ => profile.BlobTeal,
                };

                var ellipse = new Ellipse
                {
                    Fill = AuroraMesh.BlobBrush(colour, spec.Strength),
                    // The blob is oversized on purpose: a blob whose edge
                    // lands inside the viewport reads as a light spot, not
                    // as weather. Radius x2 and a centre biased toward
                    // the top-left keeps every falloff off-canvas.
                    Width = 100,
                    Height = 100,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                    IsHitTestVisible = false,
                    // The blobs are chrome, not content: they must not
                    // appear in the tab order or the accessibility tree.
                    Focusable = false,
                };

                var drift = AuroraMesh.Drift(i, metrics, motion);
                if (drift is not null) ellipse.RenderTransform = drift;

                _host.Children.Add(ellipse);
                _blobs.Add(ellipse);
            }

            LayoutBlobs();
        }

        private void LayoutBlobs()
        {
            var specs = AuroraMesh.Blobs;
            var profile = AuroraPalette.For(IsDark());
            if (profile is null || _blobs.Count == 0) return;

            double w = ActualWidth;
            double h = ActualHeight;
            if (w <= 0 || h <= 0) return;

            double spread = profile.Metrics.BlobSpread;

            for (int i = 0; i < _blobs.Count && i < specs.Count; i++)
            {
                var spec = specs[i];
                var e = _blobs[i];

                double d = Math.Max(w, h) * spec.Radius * spread;

                e.Width = d;
                e.Height = d;
                Canvas.SetLeft(e, spec.Cx * w - (d / 2));
                Canvas.SetTop(e, spec.Cy * h - (d / 2));
            }
        }

        protected override void OnSizeChanged(
            SizeChangedInfo info)
        {
            base.OnSizeChanged(info);
            // Blob geometry is host-relative, so it has to be recomputed
            // on resize. Cheap: three assignments, no re-rasterisation,
            // because every brush is frozen.
            LayoutBlobs();
        }

        /// <summary>
        /// Motion is opt-IN at the engine level and must never throw here
        /// -- a control constructor running before the painter has
        /// published would otherwise take the whole window down.
        /// </summary>
        /// <summary>
        /// Which ramp the sky is painting. Read live rather than cached,
        /// because Build() is what gets called when the theme flips.
        /// </summary>
        private static bool IsDark()
        {
            try { return ThemeManager.CurrentTheme == AppTheme.Dark; }
            catch (Exception) { return false; }
        }

        private static bool SafeMotionAllowed()
        {
            // Reduced MOTION only. Conflating this with transparency
            // suppression was a mistake repeated from the button's press
            // dip: Windows high contrast strips translucency, but it is
            // not an animation preference. The only legitimate reason to
            // freeze the sky is the user asking for less motion.
            try { return !ReducedMotion(); }
            catch (Exception) { return false; }
        }

        private static bool ReducedMotion()
        {
            try { return EightThemePainter.ReducedMotionRequested; }
            catch (Exception) { return false; }
        }
    }
}
