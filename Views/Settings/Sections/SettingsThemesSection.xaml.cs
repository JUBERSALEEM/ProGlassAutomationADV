// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/Settings/Sections/SettingsThemesSection.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: Settings — Themes page (Theme Center + Crystal Fluid tuning)
// Pure presentation — all state lives in SettingsViewModel. Loaded/Unloaded
// glue hands the VM its lifetime hooks (same pattern as the Appearance
// section). DataContext = SettingsViewModel (inherited from SettingsView).
//
// THE ONE BIT OF GLUE HERE, AND WHY IT LIVES HERE
// ───────────────────────────────────────────────────────────────────────
// The eight Theme Center cards raise a plain CLR event rather than taking
// an ICommand, because a card is bound to a DataContext item and a command
// has no reliable way to reach "which family was I" through an
// ItemsControl template without a per-item proxy command. The section owns
// the ItemsControl, so the section subscribes each generated card on
// LayoutUpdated and forwards the identity to the one write path —
// SettingsViewModel.SelectVisualTheme, which is the SAME setter the
// ComboBox uses. Two write paths is how a picker and an engine end up
// disagreeing about what is selected.
//
// The subscription is detached on Unloaded. An ItemsControl re-generates
// containers every time the page is opened, and leaving handlers attached
// to discarded cards is how a Settings page ends up raising a selection
// for a family that is three navigations old.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.ViewModels.Settings;
using ProGlassAutomation.Views.Settings.Sections.Cards;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ProGlassAutomation.Views.Settings.Sections
{
    public partial class SettingsThemesSection : UserControl
    {
        /// <summary>Cards currently subscribed. Tracked so Unloaded can
        /// detach exactly what was attached — and so a container that was
        /// recycled is never left with a stale handler.</summary>
        private readonly List<ThemeCenterCard> _subscribed = new();

        public SettingsThemesSection()
        {
            InitializeComponent();
            Loaded += OnSectionLoaded;
            Unloaded += OnSectionUnloaded;
            ThemeCenterList.LayoutUpdated += OnThemeCenterLayout;
        }

        private void OnSectionLoaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is SettingsViewModel vm)
                vm.InitializeThemesSection();
        }

        private void OnSectionUnloaded(object sender, RoutedEventArgs e)
        {
            DetachCards();
            ThemeCenterList.LayoutUpdated -= OnThemeCenterLayout;
        }

        private void OnThemeCenterLayout(object? sender, EventArgs e)
        {
            // LayoutUpdated fires constantly during scrolling, so this
            // walks the realised containers and attaches only what is new.
            // The cost is a visual-tree walk over eight items, which is
            // cheaper than it sounds and far cheaper than a per-item
            // proxy command allocation on every selection.
            AttachCards();
        }

        private void AttachCards()
        {
            foreach (var card in FindVisualChildren<ThemeCenterCard>(ThemeCenterList))
            {
                if (_subscribed.Contains(card)) continue;
                card.SelectionRequested += OnCardSelected;
                _subscribed.Add(card);
            }
        }

        private void DetachCards()
        {
            foreach (var card in _subscribed)
                card.SelectionRequested -= OnCardSelected;
            _subscribed.Clear();
        }

        private void OnCardSelected(string identity)
        {
            try
            {
                if (DataContext is SettingsViewModel vm)
                    vm.SelectVisualTheme(identity);
            }
            catch (Exception ex)
            {
                // A theme switch must never take the Settings page down.
                AppLogService.Warning(
                    $"Theme Center selection failed for '{identity}': {ex.Message}",
                    new LogContext
                    {
                        Module = "Themes",
                        Operation = "ThemeCenterSelect",
                    });
            }
        }

        private static IEnumerable<T> FindVisualChildren<T>(
            DependencyObject parent) where T : DependencyObject
        {
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typed) yield return typed;
                foreach (var nested in FindVisualChildren<T>(child))
                    yield return nested;
            }
        }
    }
}