// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Theme/VisualThemeEngine.cs
// PROJECT: ProGlassAutomation
// MODULE: Dynamic Theme Engine — visual theme families (v3)
//
// Three complete visual languages layered ON TOP of the Light/Dark
// base themes — the base brushes are derived, never hard-coded twice:
//
//   Normal        — clean, practical default (no surface overrides).
//   Monet         — colour-adaptive artistic look (Material-You style):
//                   the palette is DERIVED from the user's accent
//                   colour — hue-tinted frosted surfaces, tinted fallback
//                   gradient — and re-derives live when the accent
//                   changes. Wallpaper reads through the frost.
//   Professional  — premium corporate finish: cool slate gradient
//                   window, crisp cooler surfaces, tighter radii,
//                   restrained hover — visibly distinct from Normal.
//   iOS Liquid Glass — the app's DEFAULT look (2026-08-29 rewrite):
//                   one coherent crystal design system that adapts to
//                   Light and Dark. The window paints its own OPAQUE
//                   aurora stage (deep navy in Dark, sky-ice in Light)
//                   and every surface becomes a translucent frosted
//                   pane with crystal edges, top reflections and soft
//                   shadows — self-contained, identical on every
//                   machine, no OS backdrop dependency. (The old
//                   "Glass"/"Glassy" families were removed; saved
//                   configs referencing them resolve to this family.)
//   Ocean         — deep teal corporate.
//
// SYSTEM-WIDE WALLPAPER (v3):
//   A single global wallpaper can be set in Settings and now applies
//   to EVERY view in EVERY family: it is layered into the module area
//   (ModuleWallpaperBrush) behind the content, with surfaces made
//   translucent so it reads through. Per-module wallpapers still win
//   where configured. Typography contrast is strengthened whenever a
//   wallpaper shows through so text stays readable.
//
// Overrides are applied at APPLICATION level (they win over the merged
// theme dictionaries) and are removed when switching back to Normal —
// so nothing is ever destroyed, only layered.
//
// PUBLIC API (preserved): Normal/Monet/Professional constants,
//   Current, CurrentModule, Apply(family, wallpaperPath, opacity),
//   ApplyModule(moduleKey, appearance).
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ProGlassAutomation.Services.Theme
{
    /// <summary>Applies/removes visual theme families at runtime.</summary>
    public static partial class VisualThemeEngine
    {
    }
}
