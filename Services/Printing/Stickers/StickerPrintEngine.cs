using System;
using System.Collections.Generic;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using ProGlassAutomation.Services.Logging;

namespace ProGlassAutomation.Services.Printing.Stickers
{
    public sealed class StickerPrintResult
    {
        public bool Succeeded { get; init; }
        public bool Cancelled { get; init; }
        public string PrinterName { get; init; } = string.Empty;
        public string Message { get; init; } = string.Empty;
    }

    /// <summary>
    /// Windows/WPF custom-media print path. A label is a page; this deliberately
    /// bypasses the A4/A3 QuestPDF layouts used for business documents.
    /// </summary>
    public static class StickerPrintEngine
    {
        public static StickerPrintResult Print(
            IReadOnlyList<GlassStickerLabel> labels,
            StickerPrintPreferences preferences,
            string documentName)
        {
            if (labels == null || labels.Count == 0)
                return new StickerPrintResult { Message = "There are no glass stickers to print." };
            if (preferences == null)
                return new StickerPrintResult { Message = "Sticker print settings are missing." };

            try
            {
                var dialog = new PrintDialog
                {
                    UserPageRangeEnabled = false
                };
                if (dialog.ShowDialog() != true)
                    return new StickerPrintResult { Cancelled = true, Message = "Printing was cancelled." };

                double widthDip = StickerLabelVisualFactory.MmToDip(preferences.LabelWidthMm);
                double heightDip = StickerLabelVisualFactory.MmToDip(preferences.LabelHeightMm);
                var ticket = dialog.PrintTicket ?? new PrintTicket();
                ticket.PageMediaSize = new PageMediaSize(
                    PageMediaSizeName.Unknown, widthDip, heightDip);
                ticket.PageOrientation = preferences.LabelWidthMm >= preferences.LabelHeightMm
                    ? PageOrientation.Landscape
                    : PageOrientation.Portrait;
                dialog.PrintTicket = ticket;

                var paginator = new StickerDocumentPaginator(labels, preferences)
                {
                    PageSize = new Size(widthDip, heightDip)
                };
                if (paginator.PageCount <= 0)
                    return new StickerPrintResult { Message = "There are no valid glass pane labels to print." };

                dialog.PrintDocument(paginator, string.IsNullOrWhiteSpace(documentName)
                    ? "ProGlass Glass Stickers" : documentName);

                string printer = dialog.PrintQueue?.FullName ?? string.Empty;
                return new StickerPrintResult
                {
                    Succeeded = true,
                    PrinterName = printer,
                    Message = string.IsNullOrWhiteSpace(printer)
                        ? "Sticker batch was sent to the printer queue."
                        : "Sticker batch was sent to " + printer + "."
                };
            }
            catch (Exception ex)
            {
                const string customMediaHint = "If the printer rejects custom media, create/select the matching label size in Windows printer preferences and retry.";
                AppLogService.Error("[StickerPrinter] Custom-size print failed", ex);
                return new StickerPrintResult
                {
                    Message = "Sticker printing failed: " + ex.Message + "\n\n" + customMediaHint
                };
            }
        }
    }
}
