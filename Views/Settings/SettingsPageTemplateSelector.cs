// ═══════════════════════════════════════════════════════════════════
// FILE: Views/Settings/SettingsPageTemplateSelector.cs
// PROJECT: ProGlassAutomation
// MODULE: Settings v3 — lazy page host
//
// Picks the DataTemplate for the current SettingsPage from
// SettingsPageTemplates.xaml. The shell hosts ONE ContentControl
// (Content = this ViewModel), so a page's tree exists only while it is
// on screen: v2 stacked all twelve sections in a Grid with Visibility
// converters and paid for 6,000+ lines of XAML at window open.
//
// Keys are the NavRows registry keys ("Appearance" … "About"); the
// lookup is case-insensitive because the v2 host compared "Ai" against
// the registry's "AI" and only survived by converter accident.
// ═══════════════════════════════════════════════════════════════════

using System.Windows;
using System.Windows.Controls;
using ProGlassAutomation.ViewModels.Settings;

namespace ProGlassAutomation.Views.Settings
{
    /// <summary>Maps <see cref="SettingsViewModel.SettingsPage"/> to the
    /// page's DataTemplate (SettingsPage.&lt;key&gt;).</summary>
    public class SettingsPageTemplateSelector : DataTemplateSelector
    {
        public override DataTemplate? SelectTemplate(object? item,
            DependencyObject container)
        {
            if (item is SettingsViewModel vm && container is FrameworkElement fe)
            {
                var key = vm.SettingsPage ?? "Appearance";
                foreach (var candidate in new[] { key, char.ToUpperInvariant(key[0]) + key[1..] })
                {
                    if (fe.TryFindResource("SettingsPage." + candidate) is DataTemplate t)
                        return t;
                }

                // case-insensitive sweep of the registry keys
                foreach (var row in vm.NavRows)
                {
                    if (string.Equals(row.Key, key, System.StringComparison.OrdinalIgnoreCase)
                        && fe.TryFindResource("SettingsPage." + row.Key) is DataTemplate t)
                        return t;
                }
            }

            return base.SelectTemplate(item, container);
        }
    }
}
