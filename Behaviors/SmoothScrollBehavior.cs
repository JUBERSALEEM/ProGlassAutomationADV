// ═══════════════════════════════════════════════════════════════════════
// FILE: Behaviors/SmoothScrollBehavior.cs
// PROJECT: ProGlassAutomation
// PURPOSE: Buttery-smooth 60fps scroll behavior for ScrollViewer.
//
// ARCHITECTURE:
//   Uses CompositionTarget.Rendering frame loop (~60fps, or display
//   refresh rate on 120/144 Hz monitors) to smoothly interpolate
//   current offset toward target offset using adaptive exponential
//   easing. No DoubleAnimation, no proxy DPs, no race conditions.
//
// FEATURES:
//   • Mouse wheel — smooth eased scroll with adaptive easing
//   • Precision touchpad — respects native fine-grained delta
//   • Keyboard — Arrow keys, PgUp/PgDn, Home, End
//   • Accumulative — rapid wheel spins stack targets
//   • Modifier passthrough — Shift/Ctrl+Wheel not intercepted
//   • Zero snap-back, zero flicker, zero race conditions
//   • DUAL MODE: pixel scroll (regular content) + item scroll (virtualized)
//   • Frame-rate independent (works on 60/120/144 Hz displays)
//   • Auto-cleanup via ConditionalWeakTable (leak-proof)
//   • Skips redundant ScrollToVerticalOffset calls (perf)
//
// TUNING PRESETS (change constants at top):
//   Snappy (Chrome-like)   : WheelNotchPixels=120, EasingFactor=0.35
//   Buttery (macOS-like)   : WheelNotchPixels=100, EasingFactor=0.25
//   Instant (VS Code-like) : WheelNotchPixels=140, EasingFactor=0.50
//   Cinematic (slow)       : WheelNotchPixels=100, EasingFactor=0.18
//
// USAGE:
//   xmlns:behaviors="clr-namespace:ProGlassAutomation.Behaviors"
//   <ScrollViewer behaviors:SmoothScrollBehavior.IsEnabled="True" .../>
//
// SENIOR DEV NOTE:
//   Do NOT nest scroll surfaces. If inner content is a virtualized list,
//   set its CanContentScroll=False and let it grow inside this scroller.
//   This matches how Notion, Linear, Google Sheets handle nested content.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ProGlassAutomation.Behaviors
{
    /// <summary>
    /// Attached behavior providing smooth animated scrolling for ScrollViewer.
    /// Frame-rate independent, GC-safe, zero-allocation per frame.
    /// </summary>
    public static class SmoothScrollBehavior
    {
        // ═══════════════════════════════════════════════════════════════
        //  TUNING CONSTANTS — Chrome/Linear-like snappy default
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Pixels per mouse wheel notch for regular content.</summary>
        private const double WheelNotchPixels = 120.0;

        /// <summary>Items per mouse wheel notch for virtualized lists.</summary>
        private const double WheelNotchItems = 3.0;

        /// <summary>
        /// Base interpolation rate — % of remaining distance moved per frame.
        /// 0.35 = snappy, 0.22 = smooth, 0.50 = near-instant.
        /// </summary>
        private const double EasingFactor = 0.35;

        /// <summary>Distance below which animation snaps to target and stops.</summary>
        private const double StopThreshold = 1.0;

        /// <summary>
        /// Delta magnitude below which input is treated as precision touchpad
        /// (fine-grained scroll from Windows precision touchpads).
        /// </summary>
        private const double PrecisionThreshold = 60.0;

        /// <summary>Page scroll = viewport height × this fraction (PgUp/PgDn).</summary>
        private const double PageScrollFraction = 0.9;

        /// <summary>
        /// Adaptive easing boost — when distance is large, ease faster.
        /// Prevents laggy feel on rapid wheel spins. 1.0 = disabled.
        /// </summary>
        private const double AdaptiveEasingBoost = 1.4;

        /// <summary>Distance at which adaptive boost kicks in fully.</summary>
        private const double AdaptiveBoostThreshold = 400.0;

        /// <summary>Target frame time for 60fps reference (16.67ms).</summary>
        private const double ReferenceFrameTimeMs = 16.667;

        // ═══════════════════════════════════════════════════════════════
        //  STATE — per ScrollViewer, GC-safe via ConditionalWeakTable
        // ═══════════════════════════════════════════════════════════════

        private sealed class ScrollState
        {
            public double CurrentOffset;
            public double TargetOffset;
            public double LastScrolledOffset;
            public bool IsAnimating;
            public bool IsItemMode;
        }

        // ConditionalWeakTable auto-cleans state when ScrollViewer is GC'd.
        // Prevents leaks even if Detach() is never called (unusual scenarios).
        private static readonly ConditionalWeakTable<ScrollViewer, ScrollState>
            _states = new();

        // Weak-referenced list of active scrollers for the render loop.
        // Rebuilt on Attach/Detach — small list, cheap iteration.
        private static readonly System.Collections.Generic.List<WeakReference<ScrollViewer>>
            _activeScrollers = new();

        private static bool _renderingHooked;
        private static TimeSpan _lastRenderTime = TimeSpan.Zero;

        // ═══════════════════════════════════════════════════════════════
        //  ATTACHED PROPERTY: IsEnabled
        // ═══════════════════════════════════════════════════════════════

        public static readonly DependencyProperty IsEnabledProperty =
            DependencyProperty.RegisterAttached(
                "IsEnabled",
                typeof(bool),
                typeof(SmoothScrollBehavior),
                new PropertyMetadata(false, OnIsEnabledChanged));

        public static void SetIsEnabled(DependencyObject target, bool value)
            => target.SetValue(IsEnabledProperty, value);

        public static bool GetIsEnabled(DependencyObject target)
            => (bool)target.GetValue(IsEnabledProperty);

        private static void OnIsEnabledChanged(
            DependencyObject d,
            DependencyPropertyChangedEventArgs e)
        {
            if (d is not ScrollViewer sv) return;

            if ((bool)e.NewValue)
                Attach(sv);
            else
                Detach(sv);
        }

        // ═══════════════════════════════════════════════════════════════
        //  ATTACH / DETACH
        // ═══════════════════════════════════════════════════════════════

        private static void Attach(ScrollViewer sv)
        {
            if (_states.TryGetValue(sv, out _)) return;

            var state = new ScrollState
            {
                CurrentOffset = sv.VerticalOffset,
                TargetOffset = sv.VerticalOffset,
                LastScrolledOffset = sv.VerticalOffset,
                IsAnimating = false,
                IsItemMode = ResolveItemMode(sv)
            };

            _states.Add(sv, state);
            _activeScrollers.Add(new WeakReference<ScrollViewer>(sv));

            sv.PreviewMouseWheel += OnPreviewMouseWheel;
            sv.PreviewKeyDown += OnPreviewKeyDown;
            sv.Unloaded += OnScrollViewerUnloaded;

            if (!sv.Focusable)
                sv.Focusable = true;
        }

        private static void Detach(ScrollViewer sv)
        {
            if (!_states.TryGetValue(sv, out _)) return;

            sv.PreviewMouseWheel -= OnPreviewMouseWheel;
            sv.PreviewKeyDown -= OnPreviewKeyDown;
            sv.Unloaded -= OnScrollViewerUnloaded;

            _states.Remove(sv);

            // Purge dead + matching refs
            for (int i = _activeScrollers.Count - 1; i >= 0; i--)
            {
                if (!_activeScrollers[i].TryGetTarget(out var target) ||
                    ReferenceEquals(target, sv))
                {
                    _activeScrollers.RemoveAt(i);
                }
            }

            if (_activeScrollers.Count == 0 && _renderingHooked)
            {
                CompositionTarget.Rendering -= OnRendering;
                _renderingHooked = false;
            }
        }

        private static void OnScrollViewerUnloaded(
            object sender, RoutedEventArgs e)
        {
            if (sender is ScrollViewer sv)
                Detach(sv);
        }

        // ═══════════════════════════════════════════════════════════════
        //  MOUSE WHEEL HANDLER
        // ═══════════════════════════════════════════════════════════════


        // ═══════════════════════════════════════════════════════════════
        //  SCROLL MODE RESOLUTION — audit finding PERF-1
        //
        //  Originally this behaviour decided its mode purely from
        //  ScrollViewer.CanContentScroll:
        //
        //      IsItemMode = sv.CanContentScroll
        //
        //  That forced an unhappy trade-off on every list screen. To get
        //  smooth pixel scrolling the views set CanContentScroll="False",
        //  and WPF responds by DISABLING UI VIRTUALIZATION entirely —
        //  virtualization tracks logical items, and pixel scrolling has no
        //  concept of an item. Every row of every list was therefore
        //  realised up front. With 10,000 delivery notes that is 10,000
        //  containers built before the screen can paint.
        //
        //  Since .NET 4.5 there is a third option that the original code
        //  did not account for:
        //
        //      ScrollViewer.CanContentScroll   = True    (keeps virtualization)
        //      VirtualizingPanel.ScrollUnit    = Pixel   (keeps smooth scroll)
        //
        //  In that configuration the ScrollViewer reports CanContentScroll
        //  as True — so the old line selected ITEM mode and scrolled a
        //  whole row per wheel notch — but the panel is actually scrolling
        //  by pixel. The two disagreed, which is why simply flipping the
        //  XAML in an earlier attempt made scrolling feel wrong and was
        //  reverted.
        //
        //  ResolveItemMode asks the panel what unit is really in effect,
        //  so a list can now have BOTH virtualization and smooth pixel
        //  scrolling. Views that still use CanContentScroll="False" keep
        //  behaving exactly as before.
        // ═══════════════════════════════════════════════════════════════
        private static bool ResolveItemMode(ScrollViewer sv)
        {
            // Pixel-based physical scrolling: never item mode.
            if (!sv.CanContentScroll) return false;

            // Logical scrolling is on. If the items panel has been told to
            // scroll by pixel (the .NET 4.5 combination), honour that and
            // stay in pixel mode.
            try
            {
                if (VirtualizingPanel.GetScrollUnit(sv) == ScrollUnit.Pixel)
                    return false;
            }
            catch
            {
                // Attached property unavailable on this element — fall
                // back to the classic interpretation.
            }

            return true;
        }

        private static void OnPreviewMouseWheel(
            object sender, MouseWheelEventArgs e)
        {
            if (sender is not ScrollViewer sv) return;
            if (!_states.TryGetValue(sv, out var state)) return;

            // Passthrough for Ctrl+Wheel (zoom) / Shift+Wheel (horizontal)
            if (Keyboard.Modifiers != ModifierKeys.None) return;

            e.Handled = true;

            state.IsItemMode = ResolveItemMode(sv);

            double notchSize = state.IsItemMode
                ? WheelNotchItems
                : WheelNotchPixels;

            double delta;

            if (Math.Abs(e.Delta) < PrecisionThreshold)
            {
                // Precision touchpad — small deltas, use as-is (scaled)
                delta = state.IsItemMode
                    ? e.Delta / 40.0
                    : e.Delta * 1.2;
            }
            else
            {
                // Standard wheel — 120 units per notch
                delta = (e.Delta / 120.0) * notchSize;
            }

            // If animation is idle, sync current with actual scroll position
            if (!state.IsAnimating)
            {
                state.CurrentOffset = sv.VerticalOffset;
                state.TargetOffset = sv.VerticalOffset;
                state.LastScrolledOffset = sv.VerticalOffset;
            }

            state.TargetOffset -= delta;
            state.TargetOffset = Clamp(
                state.TargetOffset, 0, sv.ScrollableHeight);

            StartAnimation(state);
        }

        // ═══════════════════════════════════════════════════════════════
        //  KEYBOARD HANDLER
        // ═══════════════════════════════════════════════════════════════

        private static void OnPreviewKeyDown(
            object sender, KeyEventArgs e)
        {
            if (sender is not ScrollViewer sv) return;
            if (!_states.TryGetValue(sv, out var state)) return;

            state.IsItemMode = ResolveItemMode(sv);

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
                state.LastScrolledOffset = sv.VerticalOffset;
            }

            state.TargetOffset += delta;
            state.TargetOffset = Clamp(
                state.TargetOffset, 0, sv.ScrollableHeight);

            StartAnimation(state);
        }

        // ═══════════════════════════════════════════════════════════════
        //  ANIMATION — hook single render loop for all active scrollers
        // ═══════════════════════════════════════════════════════════════

        private static void StartAnimation(ScrollState state)
        {
            state.IsAnimating = true;

            if (!_renderingHooked)
            {
                _lastRenderTime = TimeSpan.Zero;
                CompositionTarget.Rendering += OnRendering;
                _renderingHooked = true;
            }
        }

        private static void OnRendering(object? sender, EventArgs e)
        {
            // Frame-time delta for frame-rate independence
            double frameFactor = 1.0;
            if (e is RenderingEventArgs args)
            {
                if (_lastRenderTime != TimeSpan.Zero)
                {
                    double elapsedMs =
                        (args.RenderingTime - _lastRenderTime).TotalMilliseconds;
                    // Cap frame factor to avoid huge jumps on tab-switch/pause
                    frameFactor = Clamp(
                        elapsedMs / ReferenceFrameTimeMs, 0.5, 3.0);
                }
                _lastRenderTime = args.RenderingTime;
            }

            bool anyStillAnimating = false;
            int deadRefs = 0;

            for (int i = 0; i < _activeScrollers.Count; i++)
            {
                if (!_activeScrollers[i].TryGetTarget(out var sv))
                {
                    deadRefs++;
                    continue;
                }

                if (!_states.TryGetValue(sv, out var state)) continue;
                if (!state.IsAnimating) continue;

                double distance = state.TargetOffset - state.CurrentOffset;
                double absDistance = Math.Abs(distance);

                if (absDistance < StopThreshold)
                {
                    // Snap to final position, stop animating
                    state.CurrentOffset = state.TargetOffset;
                    ScrollIfChanged(sv, state, state.CurrentOffset);
                    state.IsAnimating = false;
                    continue;
                }

                // Adaptive easing — boost when distance is large (rapid spin)
                double adaptiveMultiplier = 1.0;
                if (absDistance > AdaptiveBoostThreshold)
                {
                    adaptiveMultiplier = AdaptiveEasingBoost;
                }
                else if (absDistance > AdaptiveBoostThreshold * 0.5)
                {
                    // Smooth ramp between 1.0 and boost
                    double t = (absDistance - AdaptiveBoostThreshold * 0.5)
                             / (AdaptiveBoostThreshold * 0.5);
                    adaptiveMultiplier = 1.0 + (AdaptiveEasingBoost - 1.0) * t;
                }

                // Frame-rate independent easing
                double effectiveFactor = 1.0 - Math.Pow(
                    1.0 - (EasingFactor * adaptiveMultiplier),
                    frameFactor);
                effectiveFactor = Clamp(effectiveFactor, 0.0, 1.0);

                state.CurrentOffset += distance * effectiveFactor;
                ScrollIfChanged(sv, state, state.CurrentOffset);
                anyStillAnimating = true;
            }

            // Purge dead weak refs
            if (deadRefs > 0)
            {
                for (int i = _activeScrollers.Count - 1; i >= 0; i--)
                {
                    if (!_activeScrollers[i].TryGetTarget(out _))
                        _activeScrollers.RemoveAt(i);
                }
            }

            if (!anyStillAnimating && _renderingHooked)
            {
                CompositionTarget.Rendering -= OnRendering;
                _renderingHooked = false;
                _lastRenderTime = TimeSpan.Zero;
            }
        }

        /// <summary>
        /// Only calls ScrollToVerticalOffset if the pixel-rounded position
        /// actually changed. Eliminates redundant layout invalidations that
        /// occur when animation produces sub-pixel deltas.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void ScrollIfChanged(
            ScrollViewer sv, ScrollState state, double offset)
        {
            // Round to nearest pixel — WPF renders at pixel boundaries anyway
            double rounded = Math.Round(offset);
            if (Math.Abs(rounded - state.LastScrolledOffset) < 0.5)
                return;

            sv.ScrollToVerticalOffset(offset);
            state.LastScrolledOffset = rounded;
        }

        // ═══════════════════════════════════════════════════════════════
        //  UTILITY
        // ═══════════════════════════════════════════════════════════════

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double Clamp(double value, double min, double max)
            => value < min ? min : value > max ? max : value;
    }
}