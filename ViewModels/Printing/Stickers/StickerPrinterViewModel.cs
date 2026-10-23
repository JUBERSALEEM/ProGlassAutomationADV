using System;
using ProGlassAutomation.Services.Printing.Stickers;

namespace ProGlassAutomation.ViewModels.Printing.Stickers
{
    /// <summary>
    /// Main-screen design-only instance of the sticker studio. With no source
    /// rows, the shared view model supplies a clearly marked sample matching
    /// the currently selected template family.
    /// </summary>
    public sealed class StickerPrinterViewModel : StickerPrintDialogViewModel
    {
        public StickerPrinterViewModel()
            : base(Array.Empty<GlassStickerLabel>(), StickerPrintPreferencesStore.Load())
        {
        }
    }
}
