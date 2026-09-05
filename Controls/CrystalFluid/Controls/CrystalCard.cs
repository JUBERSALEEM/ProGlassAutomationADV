// ═══════════════════════════════════════════════════════════════════════
// FILE: Controls/CrystalFluid/Controls/CrystalCard.cs
// PROJECT: ProGlassAutomation
//
// IOS-17PM AUTHORED CRYSTAL CARD (P2 2026-09-05) — integrated port of
// the CrystalFluid reference implementation (incoming file 05).
//
// Defect fixes applied:
//   D2  Spurious `new` modifier on CornerRadiusProperty/CornerRadius —
//       ContentControl declares nothing of the name (CS0109 warning).
//   D5  ReducedMotion defaults from the CrystalReducedMotion token via
//       the card STYLE (DynamicResource setter); a local value still
//       wins (that is the precedence contract).
//   +   Header/Content overlap (D8) is fixed in the TEMPLATE
//       (CrystalFluidControls.xaml: DockPanel, header docked top).
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ProGlassAutomation.Controls.CrystalFluid
{
    /// <summary>
    /// A crystal glass card with optional water fill, specular reflection,
    /// and subtle hover lift. Tilt is opt-in (TiltEnabled) — the authored
    /// choreography grants it to KPI surfaces, not every card (§31).
    /// </summary>
    public class CrystalCard : ContentControl
    {
        #region ── Dependency Properties ──

        public static readonly DependencyProperty WaterLevelProperty =
            DependencyProperty.Register(
                nameof(WaterLevel),
                typeof(double),
                typeof(CrystalCard),
                new PropertyMetadata(0.0));

        public static readonly DependencyProperty WaterEnabledProperty =
            DependencyProperty.Register(
                nameof(WaterEnabled),
                typeof(bool),
                typeof(CrystalCard),
                new PropertyMetadata(false));

        public static readonly DependencyProperty BubbleEnabledProperty =
            DependencyProperty.Register(
                nameof(BubbleEnabled),
                typeof(bool),
                typeof(CrystalCard),
                new PropertyMetadata(true));

        public static readonly DependencyProperty BubbleCountProperty =
            DependencyProperty.Register(
                nameof(BubbleCount),
                typeof(int),
                typeof(CrystalCard),
                new PropertyMetadata(2));

        public static readonly DependencyProperty CausticsEnabledProperty =
            DependencyProperty.Register(
                nameof(CausticsEnabled),
                typeof(bool),
                typeof(CrystalCard),
                new PropertyMetadata(true));

        public static readonly DependencyProperty TiltEnabledProperty =
            DependencyProperty.Register(
                nameof(TiltEnabled),
                typeof(bool),
                typeof(CrystalCard),
                new PropertyMetadata(false));

        public static readonly DependencyProperty TiltMaxAngleProperty =
            DependencyProperty.Register(
                nameof(TiltMaxAngle),
                typeof(double),
                typeof(CrystalCard),
                new PropertyMetadata(1.5));

        public static readonly DependencyProperty ReducedMotionProperty =
            DependencyProperty.Register(
                nameof(ReducedMotion),
                typeof(bool),
                typeof(CrystalCard),
                new PropertyMetadata(false));

        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.Register(
                nameof(CornerRadius),
                typeof(CornerRadius),
                typeof(CrystalCard),
                new PropertyMetadata(new CornerRadius(30)));

        public static readonly DependencyProperty HeaderProperty =
            DependencyProperty.Register(
                nameof(Header),
                typeof(object),
                typeof(CrystalCard),
                new PropertyMetadata(null));

        public static readonly DependencyProperty HeaderTemplateProperty =
            DependencyProperty.Register(
                nameof(HeaderTemplate),
                typeof(DataTemplate),
                typeof(CrystalCard),
                new PropertyMetadata(null));

        #endregion

        #region ── CLR Properties ──

        public double WaterLevel
        {
            get => (double)GetValue(WaterLevelProperty);
            set => SetValue(WaterLevelProperty, value);
        }

        public bool WaterEnabled
        {
            get => (bool)GetValue(WaterEnabledProperty);
            set => SetValue(WaterEnabledProperty, value);
        }

        public bool BubbleEnabled
        {
            get => (bool)GetValue(BubbleEnabledProperty);
            set => SetValue(BubbleEnabledProperty, value);
        }

        public int BubbleCount
        {
            get => (int)GetValue(BubbleCountProperty);
            set => SetValue(BubbleCountProperty, value);
        }

        public bool CausticsEnabled
        {
            get => (bool)GetValue(CausticsEnabledProperty);
            set => SetValue(CausticsEnabledProperty, value);
        }

        public bool TiltEnabled
        {
            get => (bool)GetValue(TiltEnabledProperty);
            set => SetValue(TiltEnabledProperty, value);
        }

        public double TiltMaxAngle
        {
            get => (double)GetValue(TiltMaxAngleProperty);
            set => SetValue(TiltMaxAngleProperty, value);
        }

        public bool ReducedMotion
        {
            get => (bool)GetValue(ReducedMotionProperty);
            set => SetValue(ReducedMotionProperty, value);
        }

        public CornerRadius CornerRadius
        {
            get => (CornerRadius)GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }

        public object Header
        {
            get => GetValue(HeaderProperty);
            set => SetValue(HeaderProperty, value);
        }

        public DataTemplate HeaderTemplate
        {
            get => (DataTemplate)GetValue(HeaderTemplateProperty);
            set => SetValue(HeaderTemplateProperty, value);
        }

        #endregion

        static CrystalCard()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(CrystalCard),
                new FrameworkPropertyMetadata(typeof(CrystalCard)));
        }

        public CrystalCard()
        {
            MouseMove += OnCardMouseMove;
            MouseLeave += OnCardMouseLeave;
        }

        #region ── Hover Lift / Tilt ──

        // WPF has no 3D perspective on 2D elements without Viewport3D;
        // the authored choreography simulates it with a subtle rotation +
        // translate — deliberately restrained (§31: tilt is a KPI accent,
        // not a global effect).
        private void OnCardMouseMove(object sender, MouseEventArgs e)
        {
            if (!TiltEnabled || ReducedMotion)
                return;

            Point pos = e.GetPosition(this);
            double x = ActualWidth  > 0 ? pos.X / ActualWidth  : 0.5;
            double y = ActualHeight > 0 ? pos.Y / ActualHeight : 0.5;

            double rotateY = (x - 0.5) * TiltMaxAngle * 2;

            var transform = RenderTransform as TransformGroup;
            if (transform == null)
            {
                transform = new TransformGroup();
                transform.Children.Add(new RotateTransform());
                transform.Children.Add(new TranslateTransform(0, -3));
                RenderTransform = transform;
                RenderTransformOrigin = new Point(0.5, 0.5);
            }

            if (transform.Children[0] is RotateTransform rotate)
                rotate.Angle = rotateY * 0.5;

            if (transform.Children[1] is TranslateTransform translate)
            {
                translate.Y = -3;
                translate.X = rotateY * 0.3;
            }
        }

        private void OnCardMouseLeave(object sender, MouseEventArgs e)
        {
            if (!TiltEnabled)
                return;

            var returnAnim = new DoubleAnimation(
                0, TimeSpan.FromMilliseconds(400))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            };

            if (RenderTransform is TransformGroup group)
            {
                if (group.Children[0] is RotateTransform rotate)
                    rotate.BeginAnimation(RotateTransform.AngleProperty, returnAnim);

                if (group.Children[1] is TranslateTransform translate)
                {
                    translate.BeginAnimation(
                        TranslateTransform.XProperty, returnAnim);

                    var yAnim = new DoubleAnimation(
                        0, TimeSpan.FromMilliseconds(400))
                    {
                        EasingFunction = new CubicEase
                        {
                            EasingMode = EasingMode.EaseOut
                        }
                    };
                    translate.BeginAnimation(
                        TranslateTransform.YProperty, yAnim);
                }
            }
        }

        #endregion
    }
}
