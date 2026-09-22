// ═══════════════════════════════════════════════════════════════════
// FILE: Views/Settings/SettingsPageTemplateSelector.cs
// PROJECT: ProGlassAutomation
// MODULE: Settings v3 — lazy page host
//
// Picks the DataTemplate for the current settings page from
// SettingsPageTemplates.xaml. The shell hosts ONE ContentControl, so a
// page's tree exists only while it is on screen: v2 stacked all twelve
// sections in a Grid with Visibility converters and paid for 6,000+
// lines of XAML at window open.
//
// SYNC FIX (2026-09-17 owner report: "sidebar vs main screen sync is
// broken, options not opening"): the shell's ContentControl used
// Content="{Binding}" — the shell VM, which never changes. WPF re-runs
// a ContentTemplateSelector ONLY when Content changes, so the template
// was chosen once at load and the pane never followed the rail. The
// shell now binds Content to SettingsPage (the page KEY), which makes
// this selector receive a string and re-run on every rail click. The
// ViewModel path below is kept so any host that still passes the VM
// (or a designer that instantiates it) resolves the same template.
//
// Keys are the NavRows registry keys ("Appearance" … "About"); the
// lookup is case-insensitive because the v2 host compared "Ai" against
// the registry's "AI" and only survived by converter accident.
// ═══════════════════════════════════════════════════════════════════

using System;
using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Controls;
using ProGlassAutomation.ViewModels.Settings;

namespace ProGlassAutomation.Views.Settings
{
    /// <summary>Maps the active settings page key to its DataTemplate
    /// (SettingsPage.&lt;key&gt;). Features high-performance thread-safe
    /// template caching for instant 60fps card switching.</summary>
    public class SettingsPageTemplateSelector : DataTemplateSelector
    {
        private static readonly ConcurrentDictionary<string, DataTemplate> _cache =
            new(StringComparer.OrdinalIgnoreCase);

        public override DataTemplate? SelectTemplate(object? item,
            DependencyObject container)
        {
            if (container is not FrameworkElement fe)
                return base.SelectTemplate(item, container);

            // PRIMARY PATH — the shell passes the page key as Content.
            if (item is string key && !string.IsNullOrWhiteSpace(key))
            {
                var template = Find(fe, key);
                if (template != null) return template;
            }

            // LEGACY / DESIGNER PATH — the shell VM as Content.
            if (item is SettingsViewModel vm)
            {
                var template = Find(fe, vm.SettingsPage ?? "Appearance");
                if (template != null) return template;

                // case-insensitive sweep of the registry keys
                foreach (var row in vm.NavRows)
                {
                    if (string.Equals(row.Key, vm.SettingsPage,
                            StringComparison.OrdinalIgnoreCase) &&
                        fe.TryFindResource("SettingsPage." + row.Key) is DataTemplate t)
                    {
                        _cache[row.Key] = t;
                        return t;
                    }
                }
            }

            // Never hand back nothing: an unresolved key would leave the
            // content pane blank while the rail still highlights a row.
            return Find(fe, "Appearance") ?? base.SelectTemplate(item, container);
        }

        /// <summary>Exact key, then the same key with an upper-case first
        /// letter ("ai" → "Ai" → the registry's "AI"). Results are cached
        /// for zero-allocation instant lookups on subsequent visits.</summary>
        private static DataTemplate? Find(FrameworkElement fe, string key)
        {
            if (_cache.TryGetValue(key, out var cached))
                return cached;

            DataTemplate? found = null;
            if (fe.TryFindResource("SettingsPage." + key) is DataTemplate exact)
                found = exact;
            else if (key.Length > 0 &&
                fe.TryFindResource("SettingsPage." +
                    char.ToUpperInvariant(key[0]) + key[1..]) is DataTemplate titled)
                found = titled;

            if (found != null)
                _cache[key] = found;

            return found;
        }
    }
}
