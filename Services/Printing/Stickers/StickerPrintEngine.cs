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
        public const int MaximumBatchSize = 50_000;

        /// <summary>
        /// Shared preflight for the UI and print engine. It counts only valid
        /// source rows and rejects oversized jobs before a PrintDialog or
        /// paginator is created.
        /// </summary>
        public static bool TryValidateBatch(
            IReadOnlyList<GlassStickerLabel>? labels,
            bool oneLabelPerPiece,
            out long labelCount,
            out string error)
        {
            labelCount = 0;
            error = string.Empty;
            if (labels == null || labels.Count == 0)
            {
                error = "There are no glass stickers to print.";
                return false;
            }

            foreach (var label in labels)
            {
                if (label == null || !double.IsFinite(label.WidthMm) || !double.IsFinite(label.HeightMm)
                    || label.WidthMm <= 0 || label.HeightMm <= 0 || label.Quantity <= 0)
                    continue;

                long rowCount = oneLabelPerPiece ? label.Quantity : 1;
                if (rowCount > MaximumBatchSize - labelCount)
                {
                    labelCount += rowCount;
                    error = "This batch exceeds 50,000 labels. Filter or split the source job before printing.";
                    return false;
                }
                labelCount += rowCount;
            }

            if (labelCount == 0)
            {
                error = "There are no valid glass pane labels to print.";
                return false;
            }
            return true;
        }

        public static StickerPrintResult Print(
            IReadOnlyList<GlassStickerLabel> labels,
            StickerPrintPreferences preferences,
            string documentName) => Print(labels, preferences,
                preferences?.LastTemplate ?? StickerTemplatePresets.CreateSgu(), documentName);

        public static StickerPrintResult Print(
            IReadOnlyList<GlassStickerLabel> labels,
            StickerPrintPreferences preferences,
            StickerLayoutTemplate template,
            string documentName)
        {
            if (preferences == null)
                return new StickerPrintResult { Message = "Sticker print settings are missing." };
            if (!TryValidateBatch(labels, preferences.OneLabelPerPiece, out _, out string batchError))
                return new StickerPrintResult { Message = batchError };
            if (!StickerTemplateStore.TryValidate(template, out string templateError))
                return new StickerPrintResult { Message = "The selected sticker template is invalid: " + templateError };
            if (!StickerPrinterSpecifications.TryValidateDimensions(preferences.PrinterModel,
                    preferences.PrinterResolution, preferences.MediaHandling,
                    template.LabelWidthMm, template.LabelHeightMm, out string printerError))
                return new StickerPrintResult { Message = printerError };

            try
            {
                var dialog = new PrintDialog
                {
                    UserPageRangeEnabled = false
                };
                if (dialog.ShowDialog() != true)
                    return new StickerPrintResult { Cancelled = true, Message = "Printing was cancelled." };

                double widthDip = StickerLabelVisualFactory.MmToDip(template.LabelWidthMm);
                double heightDip = StickerLabelVisualFactory.MmToDip(template.LabelHeightMm);
                var ticket = dialog.PrintTicket ?? new PrintTicket();
                ticket.PageMediaSize = new PageMediaSize(
                    PageMediaSizeName.Unknown, widthDip, heightDip);
                ticket.PageOrientation = template.LabelWidthMm >= template.LabelHeightMm
                    ? PageOrientation.Landscape
                    : PageOrientation.Portrait;
                dialog.PrintTicket = ticket;

                var paginator = new StickerDocumentPaginator(labels, preferences, template)
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
