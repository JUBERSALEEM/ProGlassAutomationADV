using System;
using System.Collections.Generic;
using System.Windows;
using ProGlassAutomation.Services.Printing.Stickers;

namespace ProGlassAutomation.Views.Printing.Stickers
{
    /// <summary>
    /// Modal host for source-row sticker printing. The main-screen library
    /// hosts StickerPrinterView directly and does not open this window.
    /// </summary>
    public partial class StickerPrintWindow : Window
    {
        public StickerPrintWindow(
            IReadOnlyList<GlassStickerLabel> labels,
            string documentName,
            bool designOnly = false)
        {
            InitializeComponent();
            // Keep the modal inside the current desktop work area. On small
            // shop-floor displays the scrollable studio should shrink rather
            // than open with its Print/Close actions below the screen edge.
            var workArea = SystemParameters.WorkArea;
            double maxWidth = Math.Max(640, workArea.Width - 32);
            double maxHeight = Math.Max(480, workArea.Height - 32);
            MinWidth = Math.Min(MinWidth, maxWidth);
            MinHeight = Math.Min(MinHeight, maxHeight);
            Width = Math.Min(Width, maxWidth);
            Height = Math.Min(Height, maxHeight);

            Studio.InitializeForPrint(labels, documentName, designOnly);
            Closing += (_, e) =>
            {
                if (!Studio.ConfirmTemplateChange()) e.Cancel = true;
            };
        }
    }
}
