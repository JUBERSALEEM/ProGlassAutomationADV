using System.Collections.Generic;
using System.Linq;
using System.Windows;
using ProGlassAutomation.Views.Printing.Stickers;

namespace ProGlassAutomation.Services.Printing.Stickers
{
    public static class StickerPrintDialogService
    {
        public static void Show(Window? owner, IReadOnlyList<GlassStickerLabel>? labels, string documentName)
        {
            var valid = (labels ?? new List<GlassStickerLabel>())
                .Where(l => l != null && double.IsFinite(l.WidthMm) && double.IsFinite(l.HeightMm)
                    && l.WidthMm > 0 && l.HeightMm > 0 && l.Quantity > 0)
                .ToList();
            if (valid.Count == 0)
            {
                MessageBox.Show(owner,
                    "No valid glass panes were found in the current source data.",
                    "Nothing to print", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var window = new StickerPrintWindow(valid, documentName);
            if (owner != null && owner != window)
                window.Owner = owner;
            window.ShowDialog();
        }
    }
}
