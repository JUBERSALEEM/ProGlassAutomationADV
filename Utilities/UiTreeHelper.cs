// ═══════════════════════════════════════════════════════════════════════
// FILE: Utilities/UiTreeHelper.cs
// PROJECT: ProGlassAutomation
//
// Shared visual-tree helpers used by code-behind. Before this existed the
// IsDescendantOf helper was duplicated verbatim in MainWindow.xaml.cs and
// SheetStoreView.xaml.cs. Centralising it means one definition to keep
// correct (Phase 11/12 — remove duplication).
// ═══════════════════════════════════════════════════════════════════════

using System.Windows;

namespace ProGlassAutomation.Utilities
{
    /// <summary>Visual-tree helpers for UI code-behind.</summary>
    public static class UiTreeHelper
    {
        /// <summary>
        /// True when <paramref name="element"/> is a descendant of
        /// <paramref name="root"/> in the visual tree.
        /// </summary>
        public static bool IsDescendantOf(
            DependencyObject element, DependencyObject root)
        {
            DependencyObject? current = element;
            while (current != null)
            {
                if (ReferenceEquals(current, root)) return true;
                current = System.Windows.Media.VisualTreeHelper.GetParent(current);
            }
            return false;
        }
    }
}
