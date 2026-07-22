using Microsoft.Win32;

namespace ProGlassAutomation.Services.Export.FileSystem
{
    /// <summary>
    /// Centralized SaveFileDialog wrapper.
    /// Every module gets the same dialog appearance and behavior.
    /// </summary>
    public class SaveFileDialogService
    {
        public string? PromptSavePath(
            string dialogTitle,
            string suggestedFileName,
            string filterString)
        {
            var dialog = new SaveFileDialog
            {
                Title = $"Save {dialogTitle}",
                FileName = suggestedFileName,
                Filter = filterString,
                AddExtension = true,
                OverwritePrompt = true,
                RestoreDirectory = true
            };

            bool? ok = dialog.ShowDialog();
            return ok == true ? dialog.FileName : null;
        }
    }
}