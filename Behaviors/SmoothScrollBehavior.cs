using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ProGlassAutomation.Behaviors
{
    /// <summary>
    /// Buttery-smooth 60fps scroll behavior for ScrollViewer.
    /// 
    /// ARCHITECTURE:
    ///   Uses CompositionTarget.Rendering frame loop (~60fps) to smoothly
    ///   interpolate current offset toward target offset using exponential
    ///   easing. No DoubleAnimation, no proxy DPs, no race conditions.
    /// 
    /// FEATURES:
    ///   • Mouse wheel — smooth eased scroll
    ///   • Precision touchpad — respects native fine-grained delta
    ///   • Keyboard — Arrow keys, PgUp/PgDn, Home, End
    ///   • Accumulative — rapid wheel spins stack targets
    ///   • Modifier passthrough — Shift/Ctrl+Wheel not intercepted
    ///   • Zero snap-back, zero flicker, zero race conditions
    ///   • DUAL MODE: pixel scroll (regular content) + item scroll (virtualized lists)
    /// 
    /// SENIOR DEV DESIGN DECISION:
    ///   No "yield to child" logic. If you have nested scrollers, use ONE scroll
    ///   surface (outer) and don't nest another scroll region inside it.
    ///   Set the inner list to CanContentScroll=False and no MaxHeight so it
    ///   grows naturally within the outer scroller. This is how Notion, Linear,
    ///   Google Sheets, and every modern app handles nested content.
    /// 
    /// USAGE:
    ///   xmlns:behaviors="clr-namespace:ProGlassAutomation.Behaviors"
    ///   
    ///   <ScrollViewer behaviors:SmoothScrollBehavior.IsEnabled="True" ...>
    /// </summary>
    public static class SmoothScrollBehavior
    {
        // ═══════════════════════════════════════════════════════════════
        // TUNING
        // ═══════════════════════════════════════════════════════════════
        private const double WheelNotchPixels = 80.0;
        private const double WheelNotchItems = 3.0;
        private const double EasingFactor = 0.22;
        private const double StopThreshold = 0.5;
        private const double PrecisionThreshold = 60.0;
        private const double PageScrollFraction = 0.9;

        // ═══════════════════════════════════════════════════════════════
        // STATE
        // ═══════════════════════════════════════════════════════════════
        private class ScrollState
        {
            public double CurrentOffset;
            public double TargetOffset;
            public bool IsAnimating;
            public bool IsItemMode;
        }

        private static readonly Dictionary<ScrollViewer, ScrollState> _states = new();
        private static bool _renderingHooked;

        // ═══════════════════════════════════════════════════════════════
        // ATTACHED PROPERTY: IsEnabled
        // ═══════════════════════════════════════════════════════════════
        public static readonly DependencyProperty IsEnabledProperty =
            DependencyProperty.RegisterAttached(
                "IsEnabled", typeof(bool), typeof(SmoothScrollBehavior),
                new PropertyMetadata(false, OnIsEnabledChanged));

        public static void SetIsEnabled(DependencyObject target, bool value) =>
            target.SetValue(IsEnabledProperty, value);

        public static bool GetIsEnabled(DependencyObject target) =>
            (bool)target.GetValue(IsEnabledProperty);

        private static void OnIsEnabledChanged(DependencyObject d,
            DependencyPropertyChangedEventArgs e)
        {
            if (d is not ScrollViewer sv) return;

            if ((bool)e.NewValue)
                Attach(sv);
            else
                Detach(sv);
        }

        private static void Attach(ScrollViewer sv)
        {
            if (_states.ContainsKey(sv)) return;

            _states[sv] = new ScrollState
            {
                CurrentOffset = sv.VerticalOffset,
                TargetOffset = sv.VerticalOffset,
                IsAnimating = false,
                IsItemMode = sv.CanContentScroll
            };

            sv.PreviewMouseWheel += OnPreviewMouseWheel;
            sv.PreviewKeyDown += OnPreviewKeyDown;
            sv.Unloaded += OnScrollViewerUnloaded;

            if (sv.Focusable == false)
                sv.Focusable = true;
        }

        private static void Detach(ScrollViewer sv)
        {
            if (!_states.ContainsKey(sv)) return;

            sv.PreviewMouseWheel -= OnPreviewMouseWheel;
            sv.PreviewKeyDown -= OnPreviewKeyDown;
            sv.Unloaded -= OnScrollViewerUnloaded;

            _states.Remove(sv);

            if (_states.Count == 0 && _renderingHooked)
            {
                CompositionTarget.Rendering -= OnRendering;
                _renderingHooked = false;
            }
        }

        private static void OnScrollViewerUnloaded(object sender, RoutedEventArgs e)
        {
            if (sender is ScrollViewer sv)
                Detach(sv);
        }

        // ═══════════════════════════════════════════════════════════════
        // WHEEL HANDLER
        // ═══════════════════════════════════════════════════════════════
        private static void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is not ScrollViewer sv) return;
            if (!_states.TryGetValue(sv, out var state)) return;

            if (Keyboard.Modifiers != ModifierKeys.None) return;

            e.Handled = true;

            state.IsItemMode = sv.CanContentScroll;

            double notchSize = state.IsItemMode ? WheelNotchItems : WheelNotchPixels;

            double delta = Math.Abs(e.Delta) < PrecisionThreshold
                ? (state.IsItemMode ? e.Delta / 40.0 : e.Delta)
                : (e.Delta / 120.0) * notchSize;

            if (!state.IsAnimating)
            {
                state.CurrentOffset = sv.VerticalOffset;
                state.TargetOffset = sv.VerticalOffset;
            }

            state.TargetOffset -= delta;
            state.TargetOffset = Clamp(state.TargetOffset, 0, sv.ScrollableHeight);

            StartAnimation(state);
        }

        // ═══════════════════════════════════════════════════════════════
        // KEYBOARD HANDLER
        // ═══════════════════════════════════════════════════════════════
        private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (sender is not ScrollViewer sv) return;
            if (!_states.TryGetValue(sv, out var state)) return;

            state.IsItemMode = sv.CanContentScroll;
            double unitSize = state.IsItemMode ? 1.0 : WheelNotchPixels;
            double pageSize = state.IsItemMode
                ? Math.Max(1, sv.ViewportHeight * PageScrollFraction)
                : sv.ViewportHeight * PageScrollFraction;

            double delta;
            switch (e.Key)
            {
                case Key.Down: delta = unitSize; break;
                case Key.Up: delta = -unitSize; break;
                case Key.PageDown: delta = pageSize; break;
                case Key.PageUp: delta = -pageSize; break;
                case Key.Home: delta = -sv.ScrollableHeight; break;
                case Key.End: delta = sv.ScrollableHeight; break;
                default: return;
            }

            e.Handled = true;

            if (!state.IsAnimating)
            {
                state.CurrentOffset = sv.VerticalOffset;
                state.TargetOffset = sv.VerticalOffset;
            }

            state.TargetOffset += delta;
            state.TargetOffset = Clamp(state.TargetOffset, 0, sv.ScrollableHeight);

            StartAnimation(state);
        }

        // ═══════════════════════════════════════════════════════════════
        // ANIMATION
        // ═══════════════════════════════════════════════════════════════
        private static void StartAnimation(ScrollState state)
        {
            state.IsAnimating = true;

            if (!_renderingHooked)
            {
                CompositionTarget.Rendering += OnRendering;
                _renderingHooked = true;
            }
        }

        private static void OnRendering(object sender, EventArgs e)
        {
            bool anyStillAnimating = false;

            foreach (var kvp in _states)
            {
                var sv = kvp.Key;
                var state = kvp.Value;

                if (!state.IsAnimating) continue;

                double distance = state.TargetOffset - state.CurrentOffset;

                if (Math.Abs(distance) < StopThreshold)
                {
                    state.CurrentOffset = state.TargetOffset;
                    sv.ScrollToVerticalOffset(state.CurrentOffset);
                    state.IsAnimating = false;
                }
                else
                {
                    state.CurrentOffset += distance * EasingFactor;
                    sv.ScrollToVerticalOffset(state.CurrentOffset);
                    anyStillAnimating = true;
                }
            }

            if (!anyStillAnimating && _renderingHooked)
            {
                CompositionTarget.Rendering -= OnRendering;
                _renderingHooked = false;
            }
        }

        // ═══════════════════════════════════════════════════════════════
        // Utility
        // ═══════════════════════════════════════════════════════════════
        private static double Clamp(double value, double min, double max) =>
            value < min ? min : value > max ? max : value;
    }
}