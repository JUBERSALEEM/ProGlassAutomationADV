using System.Windows;
using ProGlassAutomation.Services.Theme;
using ProGlassAutomation.ThemeStudio;

namespace ProGlassAutomation.Views.Settings;

/// <summary>Hosts the imported Theme Studio inside the ERP app shell.</summary>
public sealed class ImportedThemeStudioWindow : Window
{
    private readonly ThemeStudioViewModel viewModel;

    public ImportedThemeStudioWindow()
    {
        Title = "ProGlass Theme Studio";
        Width = 1480;
        Height = 940;
        MinWidth = 980;
        MinHeight = 680;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Owner = Application.Current.MainWindow;
        viewModel = new ThemeStudioViewModel(ImportedThemeRuntime.Engine);
        Content = new ThemeStudioView { DataContext = viewModel };
        Closed += (_, _) => viewModel.Dispose();
    }

    public static void Open()
    {
        if (!ImportedThemeRuntime.IsInitialized)
            throw new InvalidOperationException("Theme runtime is not ready yet.");
        var current = Application.Current.Windows
            .OfType<ImportedThemeStudioWindow>()
            .FirstOrDefault(window => window.IsVisible);
        if (current is not null)
        {
            if (current.WindowState == WindowState.Minimized) current.WindowState = WindowState.Normal;
            current.Activate();
            return;
        }
        new ImportedThemeStudioWindow().Show();
    }
}
