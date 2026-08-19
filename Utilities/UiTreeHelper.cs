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
using System.Windows.Media;
using System.Windows.Media.Media3D;

namespace ProGlassAutomation.Utilities
{
    /// <summary>Visual-tree helpers for UI code-behind.</summary>
    public static class UiTreeHelper
    {
        /// <summary>
        /// True when <paramref name="element"/> is a descendant of
        /// <paramref name="root"/> in the visual or logical tree.
        /// ColumnDefinition / RowDefinition / FrameworkContentElement
        /// are DependencyObjects but not Visuals — VisualTreeHelper
        /// throws on them, so non-visuals walk the logical parent.
        /// </summary>
        public static bool IsDescendantOf(
            DependencyObject element, DependencyObject root)
        {
            DependencyObject? current = element;
            while (current != null)
            {
                if (ReferenceEquals(current, root)) return true;
                current = SafeGetParent(current);
            }
            return false;
        }

        public static bool IsVisual(DependencyObject? node)
            => node is Visual or Visual3D;

        public static int SafeChildrenCount(DependencyObject? node)
            => node is Visual or Visual3D
                ? VisualTreeHelper.GetChildrenCount(node)
                : 0;

        public static DependencyObject? SafeGetChild(DependencyObject node, int index)
            => node is Visual or Visual3D
                ? VisualTreeHelper.GetChild(node, index)
                : null;

        public static DependencyObject? SafeGetParent(DependencyObject? node)
        {
            if (node == null) return null;
            if (node is Visual or Visual3D)
                return VisualTreeHelper.GetParent(node);
            return LogicalTreeHelper.GetParent(node);
        }
    }
}
