// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Fonts/FontService.cs
// PROJECT: ProGlassAutomation
// MODULE: Core — global font engine (system-wide UI font)
// VERSION: 2026-08-21 — new
//
// PURPOSE:
//   One shared service that owns the app-wide UI font:
//     • lists installed system fonts + imported font files
//     • imports .ttf / .otf / .ttc / .fon files (copies them into the app
//       Fonts folder and registers them for this session via
//       AddFontResourceW + WM_FONTCHANGE — no admin, no reboot)
//     • applies a family + weight (Regular/Bold) + style (Normal/Italic)
//       globally through the AppFontFamily / AppFontWeight / AppFontStyle
//       resources (referenced with DynamicResource everywhere)
//     • persists the choice in AppConfig.Appearance and re-applies on
//       startup
//
//  WHY GDI REGISTRATION (not a permanent install):
//   WPF can only construct a FontFamily by name if the font is installed
//   or registered for the session. AddFontResourceW registers the file for
//   the current session with no administrator rights, and the WM_FONTCHANGE
//   broadcast tells every top-level window to refresh its font metrics.
//
//  SECURITY:
//   Imported files are copied into %AppData%\ProGlassAutomation\Fonts and
//   never installed system-wide. Removing a font deletes only that copy.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Common;
using ProGlassAutomation.Services.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;

namespace ProGlassAutomation.Services.Fonts
{
    /// <summary>One selectable UI font.</summary>
    public sealed class FontOption
    {
        public string Name { get; init; } = "";
        public string Source { get; init; } = "System";   // "System" | "Imported"
        public string? FilePath { get; init; }

        public string DisplaySource =>
            Source == "Imported" ? "Imported" : "System";

        public override string ToString() => Name;
    }

    /// <summary>
    /// Static facade owning the global font. Never throws — every public
    /// method returns <see cref="Result{T}"/> or a plain list.
    /// </summary>
    public static class FontService
    {
        public const string DefaultFontFamily = "Segoe UI";

        public static string FontsFolder => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ProGlassAutomation", "Fonts");

        private static readonly string[] SupportedExtensions =
            { ".ttf", ".otf", ".ttc", ".fon" };

        /// <summary>Raised (UI thread) after the global font changes.</summary>
        public static event Action? FontChanged;

        // ── GDI session font registration ─────────────────────────────
        [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
        private static extern int AddFontResourceW(string lpszFilename);

        [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
        private static extern bool RemoveFontResourceW(string lpFileName);

        [DllImport("user32.dll")]
        private static extern bool PostMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        private const int WM_FONTCHANGE = 0x001D;
        private static readonly IntPtr HWND_BROADCAST = new(0xffff);

        // ══════════════════════════════════════════════════════════════
        //  CURRENT STATE
        // ══════════════════════════════════════════════════════════════

        /// <summary>Currently applied family name.</summary>
        public static string CurrentFamily { get; private set; } = DefaultFontFamily;

        /// <summary>Currently applied weight.</summary>
        public static FontWeight CurrentWeight { get; private set; } = FontWeights.Normal;

        /// <summary>Currently applied style.</summary>
        public static FontStyle CurrentStyle { get; private set; } = FontStyles.Normal;

        /// <summary>Currently applied underline decoration.</summary>
        public static bool CurrentUnderline { get; private set; } = false;

        // ══════════════════════════════════════════════════════════════
        //  DISCOVERY & FILTERING
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Filters out emoji, symbol, wingdings, and icon glyph fonts so the
        /// user is only presented with genuine typography families.
        /// </summary>
        public static bool IsExcludedFont(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return true;
            var lower = name.ToLowerInvariant();
            return lower.Contains("emoji")
                || lower.Contains("symbol")
                || lower.Contains("wingding")
                || lower.Contains("webding")
                || lower.Contains("marlett")
                || lower.Contains("holomdl2")
                || lower.Contains("fluent")
                || lower.Contains("segoe mdl2")
                || lower.Contains("ms outlook")
                || lower.Contains("extra")
                || lower.Contains("dingbats")
                || lower.Contains("icons");
        }

        /// <summary>Installed system fonts, sorted by name, excluding icon/emoji fonts.</summary>
        public static IReadOnlyList<FontOption> GetSystemFonts()
        {
            var list = new List<FontOption>();
            try
            {
                // Fully qualified: the local namespace segment "Fonts"
                // (ProGlassAutomation.Services.Fonts) would otherwise shadow
                // System.Windows.Media.Fonts.
                foreach (var family in System.Windows.Media.Fonts.SystemFontFamilies)
                {
                    if (IsExcludedFont(family.Source)) continue;
                    list.Add(new FontOption { Name = family.Source, Source = "System" });
                }
            }
            catch (Exception ex)
            {
                AppLogService.Warning($"Could not enumerate system fonts: {ex.Message}",
                    new LogContext { Module = "Fonts", Operation = "GetSystemFonts" });
            }
            return list
                .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>Fonts previously imported into the app Fonts folder.</summary>
        public static IReadOnlyList<FontOption> GetImportedFonts()
        {
            var list = new List<FontOption>();
            try
            {
                if (!Directory.Exists(FontsFolder)) return list;

                foreach (var file in Directory.GetFiles(FontsFolder))
                {
                    if (!IsSupportedFontFile(file)) continue;
                    var name = ReadFamilyName(file);
                    if (string.IsNullOrWhiteSpace(name) || IsExcludedFont(name)) continue;
                    list.Add(new FontOption { Name = name, Source = "Imported", FilePath = file });
                }
            }
            catch (Exception ex)
            {
                AppLogService.Warning($"Could not enumerate imported fonts: {ex.Message}",
                    new LogContext { Module = "Fonts", Operation = "GetImportedFonts" });
            }
            return list
                .OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>System + imported fonts, imported first, deduplicated.</summary>
        public static IReadOnlyList<FontOption> GetAvailableFonts()
        {
            var merged = new List<FontOption>();
            merged.AddRange(GetImportedFonts());
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var f in merged) seen.Add(f.Name);

            foreach (var f in GetSystemFonts())
            {
                if (seen.Contains(f.Name)) continue;
                if (IsExcludedFont(f.Name)) continue;
                merged.Add(f);
                seen.Add(f.Name);
            }
            return merged;
        }

        // ══════════════════════════════════════════════════════════════
        //  IMPORT
        // ══════════════════════════════════════════════════════════════

        public static bool IsSupportedFontFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return false;
            var ext = Path.GetExtension(path);
            return SupportedExtensions.Any(e =>
                string.Equals(e, ext, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Reads a font file's family name, or null if unreadable.</summary>
        public static string? ReadFamilyName(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                var glyph = new GlyphTypeface(new Uri(path));
                // GlyphTypeface.FamilyNames is keyed by CultureInfo.
                if (glyph.FamilyNames.TryGetValue(
                        System.Globalization.CultureInfo.GetCultureInfo("en-us"),
                        out var name) && !string.IsNullOrWhiteSpace(name))
                    return name;

                return glyph.FamilyNames.Values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Imports a font file: validates, copies it into the app Fonts
        /// folder, registers it for this session and returns its family name.
        /// </summary>
        public static Result<string> ImportFont(string sourcePath, string? correlationId = null)
        {
            var corrId = correlationId ?? CorrelationContext.NewId();
            using var scope = CorrelationContext.BeginScope(corrId);

            try
            {
                if (string.IsNullOrWhiteSpace(sourcePath))
                    return Result<string>.Fail("No file was selected.", corrId);
                if (!File.Exists(sourcePath))
                    return Result<string>.Fail("The selected file no longer exists.", corrId);
                if (!IsSupportedFontFile(sourcePath))
                    return Result<string>.Fail(
                        "Unsupported font file. Use .ttf, .otf, .ttc or .fon.", corrId);

                string family = ReadFamilyName(sourcePath)
                    ?? throw new InvalidOperationException("The file is not a readable font.");

                Directory.CreateDirectory(FontsFolder);
                string ext = Path.GetExtension(sourcePath).ToLowerInvariant();
                string target = Path.Combine(FontsFolder, SanitizeFileName(family) + ext);
                File.Copy(sourcePath, target, overwrite: true);

                int added = AddFontResourceW(target);
                if (added == 0)
                {
                    AppLogService.Warning($"AddFontResourceW returned 0 for {target}",
                        new LogContext { Module = "Fonts", Operation = "Import", CorrelationId = corrId });
                    // Registration is best-effort: the file is still imported
                    // and can be applied after the next restart.
                }
                else
                {
                    BroadcastFontChange();
                }

                AppLogService.Info($"Font imported: {family} → {target}",
                    new LogContext { Module = "Fonts", Operation = "Import", CorrelationId = corrId });
                return Result<string>.Ok(family, corrId);
            }
            catch (Exception ex)
            {
                var errId = AppLogService.Error($"Font import failed: {ex.Message}", ex,
                    new LogContext { Module = "Fonts", Operation = "Import", CorrelationId = corrId });
                return Result<string>.Fail($"Could not import font: {ex.Message}", ex, corrId, errId);
            }
        }

        /// <summary>Removes an imported font file (never a system font).</summary>
        public static Result RemoveImportedFont(string familyName, string? correlationId = null)
        {
            var corrId = correlationId ?? CorrelationContext.NewId();
            try
            {
                var match = GetImportedFonts().FirstOrDefault(f =>
                    string.Equals(f.Name, familyName, StringComparison.OrdinalIgnoreCase));
                if (match == null || string.IsNullOrWhiteSpace(match.FilePath))
                    return Result.Fail("That font is not an imported font.", corrId);

                RemoveFontResourceW(match.FilePath);
                File.Delete(match.FilePath);
                BroadcastFontChange();

                return Result.Ok(corrId);
            }
            catch (Exception ex)
            {
                var errId = AppLogService.Error($"Remove font failed: {ex.Message}", ex,
                    new LogContext { Module = "Fonts", Operation = "Remove", CorrelationId = corrId });
                return Result.Fail(ex.Message, ex, corrId, errId);
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  APPLY
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Applies a family + weight + style + underline app-wide via the shared
        /// AppFontFamily / AppFontWeight / AppFontStyle / AppTextDecorations resources.
        /// Persists the choice and raises <see cref="FontChanged"/>.
        /// </summary>
        public static Result ApplyFont(
            string familyName, FontWeight weight, FontStyle style, bool underline,
            bool persist = true, string? correlationId = null)
        {
            var corrId = correlationId ?? CorrelationContext.NewId();
            try
            {
                string name = string.IsNullOrWhiteSpace(familyName) ? DefaultFontFamily : familyName.Trim();

                // If the family is neither installed nor imported, fall back —
                // WPF itself would silently render an unknown name in the
                // default face, so pick Segoe UI explicitly instead.
                if (!FontExists(name))
                    name = DefaultFontFamily;

                CurrentFamily = name;
                CurrentWeight = weight;
                CurrentStyle = style;
                CurrentUnderline = underline;

                var app = Application.Current;
                if (app != null)
                {
                    app.Resources["AppFontFamily"] = new FontFamily(name);
                    app.Resources["AppFontWeight"] = weight;
                    app.Resources["AppFontStyle"] = style;
                    app.Resources["AppTextDecorations"] = underline ? TextDecorations.Underline : null;
                }

                if (persist && App.Config?.Appearance != null)
                {
                    App.Config.Appearance.FontFamilyName = name;
                    App.Config.Appearance.FontWeightName =
                        weight == FontWeights.Bold ? "Bold" : "Normal";
                    App.Config.Appearance.FontStyleName =
                        style == FontStyles.Italic ? "Italic" : "Normal";
                    App.Config.Appearance.FontUnderline = underline;
                    Services.AI.AIConfig.ConfigService.Save(App.Config);
                }

                RaiseFontChanged();
                return Result.Ok(corrId);
            }
            catch (Exception ex)
            {
                var errId = AppLogService.Error($"ApplyFont failed: {ex.Message}", ex,
                    new LogContext { Module = "Fonts", Operation = "Apply", CorrelationId = corrId });
                return Result.Fail(ex.Message, ex, corrId, errId);
            }
        }

        /// <summary>
        /// Backward-compatible overload for 3-argument callers.
        /// </summary>
        public static Result ApplyFont(
            string familyName, FontWeight weight, FontStyle style,
            bool persist = true, string? correlationId = null)
            => ApplyFont(familyName, weight, style, false, persist, correlationId);

        /// <summary>Resets to Segoe UI / Normal / Normal without underline and clears persistence.</summary>
        public static Result ResetToDefault(string? correlationId = null)
        {
            return ApplyFont(DefaultFontFamily, FontWeights.Normal, FontStyles.Normal, false,
                persist: true, correlationId: correlationId);
        }

        /// <summary>Re-applies the persisted font (call once at startup).</summary>
        public static void ApplySavedFont()
        {
            try
            {
                var a = App.Config?.Appearance;
                if (a == null) return;

                FontWeight weight = string.Equals(a.FontWeightName, "Bold", StringComparison.OrdinalIgnoreCase)
                    ? FontWeights.Bold : FontWeights.Normal;
                FontStyle style = string.Equals(a.FontStyleName, "Italic", StringComparison.OrdinalIgnoreCase)
                    ? FontStyles.Italic : FontStyles.Normal;
                bool underline = a.FontUnderline;

                ApplyFont(a.FontFamilyName, weight, style, underline, persist: false);
            }
            catch (Exception ex)
            {
                AppLogService.Warning($"ApplySavedFont failed: {ex.Message}",
                    new LogContext { Module = "Fonts", Operation = "ApplySavedFont" });
            }
        }

        /// <summary>
        /// True when the family name matches an installed system font or an
        /// imported font file. (WPF does not throw for unknown names — it
        /// silently falls back — so this checks against the real lists.)
        /// </summary>
        public static bool FontExists(string familyName)
        {
            if (string.IsNullOrWhiteSpace(familyName)) return false;

            try
            {
                foreach (var f in GetAvailableFonts())
                    if (string.Equals(f.Name, familyName, StringComparison.OrdinalIgnoreCase))
                        return true;
            }
            catch
            {
                // Enumeration failed — fall back to trying the family directly.
                try { return new FontFamily(familyName).Source != null; }
                catch { return false; }
            }
            return false;
        }

        // ══════════════════════════════════════════════════════════════
        //  HELPERS
        // ══════════════════════════════════════════════════════════════

        public static FontWeight ParseWeight(string? name) =>
            string.Equals(name, "Bold", StringComparison.OrdinalIgnoreCase)
                ? FontWeights.Bold : FontWeights.Normal;

        public static FontStyle ParseStyle(string? name) =>
            string.Equals(name, "Italic", StringComparison.OrdinalIgnoreCase)
                ? FontStyles.Italic : FontStyles.Normal;

        private static string SanitizeFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var cleaned = new string(name.Where(c => !invalid.Contains(c)).ToArray());
            return string.IsNullOrWhiteSpace(cleaned) ? "ImportedFont" : cleaned;
        }

        private static void BroadcastFontChange()
        {
            try { PostMessage(HWND_BROADCAST, WM_FONTCHANGE, IntPtr.Zero, IntPtr.Zero); }
            catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[FontService] BroadcastFontChange: broadcast is best-effort: " + ex.Message); }
        }

        private static void RaiseFontChanged()
        {
            try
            {
                var disp = Application.Current?.Dispatcher;
                if (disp != null && !disp.CheckAccess())
                    disp.InvokeAsync(() => { try { FontChanged?.Invoke(); } catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); } });
                else
                    FontChanged?.Invoke();
            }
            catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Warning("[FontService] RaiseFontChanged: UI must never break the font engine: " + ex.Message); }
        }
    }
}
