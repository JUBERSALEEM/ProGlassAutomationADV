// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Theme/VisualThemeEngine.cs
// PROJECT: ProGlassAutomation
// MODULE: Dynamic Theme Engine — visual theme families (v3)
//
// Four visual families layered ON TOP of the Light/Dark base themes —
// the base brushes are derived, never hard-coded twice (2026-09-06 theme
// reduction removed Professional, iOS Liquid Glass, Ocean and the
// Industrial trio; 2026-09-07 restores the pre-cream industrial "Normal"
// as its own family, "Normal (Industrial)"):
//
//   Normal            — clean, practical cream SaaS DEFAULT (no surface
//                       overrides).
//   Normal (Industrial) — the PRE-CREAM "Normal" (August-26 look) restored
//                       as a standalone family: full white + blue tone,
//                       professional industrial layout, and a defined
//                       slate card edge/corner border line. See
//                       design-system/proglass-industrial/MASTER.md.
//   Monet          — colour-adaptive artistic look (Material-You style):
//                    the palette is DERIVED from the user's accent colour
//                    — hue-tinted frosted surfaces, tinted fallback
//                    gradient — and re-derives live when the accent
//                    changes. Wallpaper reads through the frost.
//   Crystal Fluid  — displayed as "Ios-17pm": the iOS water-glass design
//                    system. The window paints its own OPAQUE pale stage
//                    and every surface becomes a translucent crystal pane
//                    with crystal edges, top reflections and soft
//                    shadows. Light-only (ModePairFor → false); see
//                    Themes/CrystalFluid/.
//
// Saved configs referencing any removed family (or any unknown value)
// resolve to Normal — the new default. Legacy industrial names
// ("Professional", "Industrial White/Grey/Dark") resolve back to
// "Normal (Industrial)" so pre-reduction installs keep their blue
// industrial look.
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
