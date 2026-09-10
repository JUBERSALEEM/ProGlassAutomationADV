using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Views.Shared.Dialogs;
using System.Collections.Generic;
using System.Windows;

namespace ProGlassAutomation.Views.Shared
{
    /// <summary>
    /// GitHub-style before/after viewer for audit change sets.
    /// Usage: ChangesDialog.Show(owner, "PI-2026-001 — edited", changes);
    /// </summary>
    public partial class ChangesDialog : ThemedDialogWindow
    {
        private ChangesDialog()
        {
            InitializeComponent();
        }

        /// <summary>Opens the dialog when the change set is non-empty.</summary>
        public static void Show(
            Window? owner, string subtitle, List<AuditChange> changes)
        {
            if (changes == null || changes.Count == 0)
            {
                ThemedMessageBox.Show(
                    owner,
                    "This entry has no field-level change details.",
                    "Changes",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var dialog = new ChangesDialog { Owner = owner };
            dialog.DialogSubtitle = subtitle;
            dialog.ChangesList.ItemsSource = changes;
            dialog.ShowDialog();
        }
    }
}
