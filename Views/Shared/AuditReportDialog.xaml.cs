using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.ViewModels.Shared;
using ProGlassAutomation.Views.Shared.Dialogs;
using System.Collections.Generic;
using System.Windows;

namespace ProGlassAutomation.Views.Shared
{
    /// <summary>
    /// Professional who/when/what/why audit report for one activity row.
    /// </summary>
    public partial class AuditReportDialog : ThemedDialogWindow
    {
        private AuditReportDialog()
        {
            InitializeComponent();
        }

        public static void Show(
            Window? owner,
            ActivityRowViewModel row,
            List<AuditChange> changes)
        {
            var dialog = new AuditReportDialog
            {
                Owner = owner,
                DataContext = row
            };
            dialog.ChangesList.ItemsSource = changes ?? new List<AuditChange>();
            dialog.ShowDialog();
        }

        private void CopyReport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (DataContext is ActivityRowViewModel row)
                    Clipboard.SetText(row.AuditReportText);
            }
            catch
            {
                // Clipboard can be locked by another process; reporting must not crash.
            }
        }
    }
}
