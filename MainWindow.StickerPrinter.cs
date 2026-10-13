using System;
using System.Windows;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.Printing.Stickers;
using ProGlassAutomation.Views.Printing.Stickers;

namespace ProGlassAutomation
{
    public partial class MainWindow
    {
        private void OpenStickerDesigner_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var window = new StickerPrintWindow(
                    Array.Empty<GlassStickerLabel>(),
                    "Sticker Template Library",
                    designOnly: true)
                {
                    Owner = this
                };
                window.ShowDialog();
            }
            catch (Exception ex)
            {
                AppLogService.Error("[StickerPrinter] Could not open the standalone template library", ex);
                MessageBox.Show(this,
                    "Could not open the sticker template library." + Environment.NewLine
                    + Environment.NewLine + ex.Message,
                    "Sticker printer", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
