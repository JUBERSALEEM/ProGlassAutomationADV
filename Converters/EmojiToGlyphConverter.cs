// ═══════════════════════════════════════════════════════════════════════
// FILE: Converters/EmojiToGlyphConverter.cs
// PROJECT: ProGlassAutomation
// MODULE: Shared — emoji → Segoe MDL2 Assets glyph (v3 standardization)
//
// The v3 button standard bans emoji in buttons/tabs/badges; icons are
// Segoe MDL2 Assets glyphs. Some icon strings come from ViewModels
// (e.g. Diagnostics tab icons) — this converter maps them at the XAML
// layer so those ViewModels stay untouched (pure-XAML refactor rule).
//
// Usage:
//   <TextBlock Text="{Binding Icon, Converter={StaticResource EmojiToGlyph}}"
//              FontFamily="Segoe MDL2 Assets"/>
//
// Unknown strings pass through unchanged and are logged for the map.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows.Data;

namespace ProGlassAutomation.Converters
{
    /// <summary>Maps emoji icon strings to Segoe MDL2 Assets glyphs.</summary>
    public sealed class EmojiToGlyphConverter : IValueConverter
    {
        private static readonly Dictionary<string, string> Map = new()
        {
            ["↻"] = "\xE72C", ["🔄"] = "\xE72C",
            ["🕘"] = "\xE81C", ["🕐"] = "\xE81C", ["📜"] = "\xE81C",
            ["📅"] = "\xE787",
            ["📤"] = "\xE72D", ["⤴"] = "\xE72D",
            ["💾"] = "\xE74E",
            ["🗑"] = "\xE74D", ["❌"] = "\xE711",
            ["✎"] = "\xE8E5", ["✏"] = "\xE8E5",
            ["➕"] = "\xE710", ["+"] = "\xE710",
            ["✕"] = "\xE711",
            ["🔍"] = "\xE721",
            [""] = "\xE71C", ["🎚"] = "\xE71C",
            ["⬇"] = "\xE896",
            ["⬆"] = "\xE898",
            ["🖨"] = "\xE749",
            ["▦"] = "\xE9F9", ["📊"] = "\xE9F9",
            ["📄"] = "\xE8A5",
            ["⚙"] = "\xE713", ["🛠"] = "\xE713",
            ["ⓘ"] = "\xE946",
            ["⚠"] = "\xE7BA",
            ["✓"] = "\xE73E", ["✅"] = "\xE73E",
            ["👥"] = "\xE716",
            ["👤"] = "\xE77B",
            ["📡"] = "\xE839",
            ["🔁"] = "\xE895",
            ["📦"] = "\xE7B8",
            ["📋"] = "\xE8C8",
            ["🏢"] = "\xE822",
            ["📂"] = "\xE8B7",   // FolderOpen — map extension (not in v1 table)
            ["🧾"] = "\xE8A5",   // receipt-ish -> Document
            ["🩺"] = "\xE946",   // diagnostics -> Info
            ["🕓"] = "\xE81C",
        };

        public object Convert(object value, Type targetType,
            object parameter, CultureInfo culture)
        {
            var s = value as string ?? string.Empty;
            s = s.Trim();

            if (s.Length == 0) return s;

            if (Map.TryGetValue(s, out var glyph)) return glyph;

            // Multi-char labels ("📂  Open log folder"): convert a leading
            // emoji and drop it (labels belong in Content, not the icon).
            foreach (var key in Map.Keys)
            {
                if (s.StartsWith(key, StringComparison.Ordinal))
                    return Map[key];
            }

            return s; // unknown — passes through, extend the map
        }

        public object ConvertBack(object value, Type targetType,
            object parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
