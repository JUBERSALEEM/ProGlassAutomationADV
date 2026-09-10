using System.Windows;
using ProGlassAutomation.Views.Shared.Dialogs;

namespace ProGlassAutomation.Views.Shared
{
    public enum UnsavedChangesChoice
    {
        Cancel,
        Discard,
        SaveAndContinue
    }

    /// <summary>
    /// Cancel / Discard / Save &amp; Continue confirmation (§28).
    /// </summary>
    public partial class UnsavedChangesDialog : ThemedDialogWindow
    {
        private UnsavedChangesChoice _choice = UnsavedChangesChoice.Cancel;

        private UnsavedChangesDialog()
        {
            InitializeComponent();

            CancelButton.Click += (_, __) =>
            {
                _choice = UnsavedChangesChoice.Cancel;
                Close();
            };

            DiscardButton.Click += (_, __) =>
            {
                _choice = UnsavedChangesChoice.Discard;
                Close();
            };

            SaveButton.Click += (_, __) =>
            {
                _choice = UnsavedChangesChoice.SaveAndContinue;
                Close();
            };
        }

        /// <summary>Shows the dialog and returns the user's choice.</summary>
        public static UnsavedChangesChoice Show(
            Window? owner, string documentDescription)
        {
            var dialog = new UnsavedChangesDialog { Owner = owner };
            dialog.MessageText.Text =
                $"Your {documentDescription} has changes that are not " +
                "saved yet. What would you like to do?";
            dialog.ShowDialog();
            return dialog._choice;
        }
    }
}
