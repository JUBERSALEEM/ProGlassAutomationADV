// ═══════════════════════════════════════════════════════════════════════
// FILE: Controls/Claymorphism/ClayPressHost.cs
// PROJECT: ProGlassAutomation
// MODULE: Claymorphism — the press spring, wired to a template
//
// Every pressable clay control needs the same three things:
//
//   1. find the two transforms its template declared
//   2. react to IsPressed
//   3. hand the change to ClayMotion, which owns the spring and the
//      reduced-motion branch
//
// Written three times it would be written three times slightly
// differently, and "slightly differently" is how a button ends up with
// no press feedback at all while a pill next to it springs perfectly.
// Internal: controls own one, callers never do.
//
// ── WHY IsPressed IS POLLED RATHER THAN SUBSCRIBED ───────────────────
// IsPressed is a DP on ButtonBase. It raises no routed event, so the
// only way to hear it without re-templating the input system is
// DependencyPropertyDescriptor. That is also why the recipe repaints on
// the same property: one subscription, one place to be wrong.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;

using ProGlassAutomation.Services.Theme.Clay;

namespace ProGlassAutomation.Controls.Claymorphism
{
    /// <summary>Drives a clay template's press spring. Internal.</summary>
    internal sealed class ClayPressHost
    {
        private ScaleTransform? _scale;
        private TranslateTransform? _translate;
        private Storyboard? _running;
        private bool _hooked;

        /// <summary>Scale reached while pressed.</summary>
        public double PressedScale { get; set; } = 0.98;

        /// <summary>Pixels the control travels into the page.</summary>
        public double PressedDepth { get; set; } = 2.0;

        /// <summary>
        /// True once both transforms were found. A template missing them
        /// gets no motion rather than a crash — a silent degrade is
        /// correct here because the PRESS STILL READS through the
        /// recipe's own state change, and a missing transform is a
        /// template bug that should surface as "no animation", not as an
        /// exception from a pointer event.
        /// </summary>
        public bool Complete => _scale is not null && _translate is not null;

        /// <summary>Resolves the transforms from an applied template.</summary>
        public void Bind(Control root, string scaleName, string translateName)
        {
            if (root.Template is null) return;

            _scale = root.Template.FindName(scaleName, root) as ScaleTransform;
            _translate = root.Template.FindName(
                translateName, root) as TranslateTransform;

            if (_hooked) return;
            if (!Complete) return;

            _hooked = true;
            DependencyPropertyDescriptor.FromProperty(
                    ButtonBase.IsPressedProperty, typeof(ButtonBase))
                .AddValueChanged(root, (_, _) => Sync(root));

            // A control can be mid-press when it loses the capture (a drag
            // that leaves the window, or a context menu opening). Without
            // this the button stays pushed until the next press.
            root.LostMouseCapture += (_, _) => Sync(root);
        }

        /// <summary>Drives the spring to match the control's real state.</summary>
        public void Sync(Control root)
        {
            if (!Complete) return;

            // An animation already in flight owns the property. Starting a
            // second one mid-flight makes WPF queue a BeginTime we never
            // resolve, which leaves the control stuck part-way through a
            // press until the next interaction.
            _running?.Stop();
            _running = null;

            ClayMotion.Press(
                _scale!, _translate!,
                root.IsEnabled && (root as ButtonBase)?.IsPressed == true,
                ClayMotion.DefaultEase,
                PressedScale, PressedDepth);
        }

        /// <summary>
        /// Snaps the transforms back to rest with no animation. Used when
        /// the control is torn down or disabled: a disabled control must
        /// not be left holding the pressed transform, because there is no
        /// IsPressed change coming to bring it back.
        /// </summary>
        public void Reset()
        {
            if (!Complete) return;
            _running?.Stop();
            _running = null;
            _scale!.BeginAnimation(
                ScaleTransform.ScaleXProperty, null);
            _scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            _translate!.BeginAnimation(TranslateTransform.YProperty, null);
            _scale.ScaleX = 1.0;
            _scale.ScaleY = 1.0;
            _translate.Y = 0.0;
        }
    }
}
