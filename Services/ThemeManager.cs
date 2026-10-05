// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/ThemeManager.cs
// PROJECT: ProGlassAutomation
// MODULE: Dynamic Theme Engine
//
// UPGRADE — "adapt colour more wisely"
// ─────────────────────────────────────────────────────────────────────
// The original manager only swapped Light/Dark dictionaries. It kept
// every accent colour hard-coded, so switching the brand accent meant
// editing a dozen hex values by hand — and nothing guaranteed that
// hover/pressed/tint variants stayed consistent or readable.
//
// The upgraded engine:
//   1. Still swaps Light/Dark dictionaries (ApplyTheme — unchanged API).
//   2. Derives the ENTIRE accent family from ONE colour using HSL math
//      (Services/Theme/ColorMath.cs): hover, dark, light, overlay and
//      tint variants — all computed, never hard-coded.
//   3. Picks text-on-accent colour by WCAG contrast so buttons are
//      always readable, even if someone chooses a yellow accent.
//   4. Re-derives automatically after every light↔dark swap so the
//      accent always sits correctly against the active surfaces.
//   5. Publishes card-shape + motion resources that the rest of the
//      UI binds to via DynamicResource (CornerRadius, durations).
//
// PUBLIC API (all preserved — nothing removed):
//   AppTheme enum, CurrentTheme, ThemeChanged,
//   ApplyTheme(theme), ToggleTheme()
// NEW API:
//   AccentColor, AccentChanged, ApplyAccent(color), ResetAccent()
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Theme;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace ProGlassAutomation.Services
{
    public enum AppTheme
    {
        Light,
        Dark
    }

    public static partial class ThemeManager
    {
    }
}
