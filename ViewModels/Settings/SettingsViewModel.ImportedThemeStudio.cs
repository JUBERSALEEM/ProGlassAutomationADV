using System.Windows;
using System.Windows.Input;
using ProGlassAutomation.Commands;
using ProGlassAutomation.Views.Settings;

namespace ProGlassAutomation.ViewModels.Settings;

public sealed partial class SettingsViewModel
{
    private ICommand? openImportedThemeStudioCommand;

    public ICommand OpenImportedThemeStudioCommand =>
        openImportedThemeStudioCommand ??= new RelayCommand(_ =>
        {
            try { ImportedThemeStudioWindow.Open(); }
            catch (Exception ex)
            {
                Services.Dialogs.Prompts.Show(
                    "Theme Studio could not be opened. The current application theme remains active.\n\n" + ex.Message,
                    "Theme Studio", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        });
}
