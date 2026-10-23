using System;
using System.Collections.Generic;
using System.Linq;
using System.Printing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using ProGlassAutomation.Services.Logging;

namespace ProGlassAutomation.Services.Printing.Stickers
{
    public sealed class StickerPrintResult
    {
        public bool Succeeded { get; init; }
        public bool Cancelled { get; init; }
        public string PrinterName { get; init; } = string.Empty;
        public string Message { get; init; } = string.Empty;
        public long StickerCount { get; init; }
        public long PageCount { get; init; }
    }

    /// <summary>
    /// Windows/WPF sticker print path. Thermal/roll workflows keep one custom
    /// media page per sticker; standard-printer workflows can arrange true-size
    /// stickers onto printer-capability-aware A4 sheets.
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
                if (!StickerLabelSequence.IsPrintable(label)) continue;

                long rowCount = oneLabelPerPiece ? label!.Quantity : 1;
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
            string documentName,
            Window? owner = null) => Print(labels, preferences,
                preferences?.LastTemplate ?? StickerTemplatePresets.CreateSgu(), documentName, owner);

        public static StickerPrintResult Print(
            IReadOnlyList<GlassStickerLabel> labels,
            StickerPrintPreferences preferences,
            StickerLayoutTemplate template,
            string documentName,
            Window? owner = null)
        {
            if (preferences == null)
                return new StickerPrintResult { Message = "Sticker print settings are missing." };
            if (!TryValidateBatch(labels, preferences.OneLabelPerPiece,
                    out long stickerCount, out string batchError))
                return new StickerPrintResult { Message = batchError };
            if (!StickerTemplateStore.TryValidate(template, out string templateError))
                return new StickerPrintResult { Message = "The selected sticker template is invalid: " + templateError };
            if (!StickerPrinterSpecifications.TryValidateDimensions(preferences.PrinterModel,
                    preferences.PrinterResolution, preferences.MediaHandling,
                    template.LabelWidthMm, template.LabelHeightMm, out string printerError))
                return new StickerPrintResult { Message = printerError };

            StickerOutputLayoutMode outputLayout = ResolveOutputLayout(preferences);
            if (preferences.PrinterModel == StickerPrinterModel.ZebraZt411
                && outputLayout == StickerOutputLayoutMode.A4Sheet)
                return new StickerPrintResult
                {
                    Message = "A4 sheet layout is for standard printers. Select one sticker per page for the Zebra ZT411 profile."
                };

            try
            {
                var dialog = new PrintDialog
                {
                    UserPageRangeEnabled = false
                };
                if (dialog.ShowDialog() != true)
                    return new StickerPrintResult { Cancelled = true, Message = "Printing was cancelled." };

                DocumentPaginator paginator;
                long pageCount;
                string layoutSummary;
                if (outputLayout == StickerOutputLayoutMode.A4Sheet)
                {
                    if (!TryCreateA4SheetPlan(dialog, preferences, template,
                            out var ticket, out var plan, out bool usedConservativeMargins,
                            out string planError))
                        return new StickerPrintResult { Message = planError };

                    dialog.PrintTicket = ticket;
                    var sheetPaginator = new StickerSheetDocumentPaginator(
                        labels, preferences, template, plan);
                    if (sheetPaginator.PageCountLong <= 0)
                        return new StickerPrintResult { Message = "There are no valid glass pane labels to print." };

                    layoutSummary = plan.Columns + " columns × " + plan.Rows + " rows; "
                        + plan.LabelsPerSheet + " stickers per A4 sheet, "
                        + sheetPaginator.PageCountLong.ToString("N0") + " sheet(s), "
                        + plan.Orientation.ToString().ToLowerInvariant();
                    if (usedConservativeMargins)
                        layoutSummary += ". The driver did not report an imageable area, so a conservative 10 mm printer margin was used";

                    string confirmation = stickerCount.ToString("N0") + " stickers will print on "
                        + sheetPaginator.PageCountLong.ToString("N0") + " A4 sheet(s).\n\n"
                        + "Arrangement: " + layoutSummary + ".\n"
                        + "The app preserves each sticker's designed dimensions. Confirm the driver is not set to Fit to page or other scaling. Continue?";
                    if (MessageBox.Show(owner, confirmation, "Confirm A4 sticker layout",
                            MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No)
                        != MessageBoxResult.Yes)
                        return new StickerPrintResult
                        {
                            Cancelled = true,
                            StickerCount = stickerCount,
                            PageCount = sheetPaginator.PageCountLong,
                            Message = "Printing was cancelled after reviewing the A4 sheet layout."
                        };

                    paginator = sheetPaginator;
                    pageCount = sheetPaginator.PageCountLong;
                }
                else
                {
                    double widthDip = StickerLabelVisualFactory.MmToDip(template.LabelWidthMm);
                    double heightDip = StickerLabelVisualFactory.MmToDip(template.LabelHeightMm);
                    var ticket = dialog.PrintTicket ?? new PrintTicket();
                    ticket.PageMediaSize = new PageMediaSize(
                        PageMediaSizeName.Unknown, widthDip, heightDip);
                    ticket.PageOrientation = template.LabelWidthMm >= template.LabelHeightMm
                        ? PageOrientation.Landscape
                        : PageOrientation.Portrait;
                    dialog.PrintTicket = ticket;

                    var labelPaginator = new StickerDocumentPaginator(labels, preferences, template)
                    {
                        PageSize = new Size(widthDip, heightDip)
                    };
                    if (labelPaginator.PageCountLong <= 0)
                        return new StickerPrintResult { Message = "There are no valid glass pane labels to print." };

                    paginator = labelPaginator;
                    pageCount = labelPaginator.PageCountLong;
                    layoutSummary = pageCount.ToString("N0") + " individual sticker page(s)";
                }

                dialog.PrintDocument(paginator, string.IsNullOrWhiteSpace(documentName)
                    ? "ProGlass Glass Stickers" : documentName);

                string printer = dialog.PrintQueue?.FullName ?? string.Empty;
                string destination = string.IsNullOrWhiteSpace(printer) ? "the printer queue" : printer;
                return new StickerPrintResult
                {
                    Succeeded = true,
                    PrinterName = printer,
                    StickerCount = stickerCount,
                    PageCount = pageCount,
                    Message = stickerCount.ToString("N0") + " stickers sent to " + destination
                        + " as " + layoutSummary + "."
                };
            }
            catch (Exception ex)
            {
                string hint = outputLayout == StickerOutputLayoutMode.A4Sheet
                    ? "Confirm the selected driver supports A4, disable driver scaling such as Fit to page, and check that its printable margins match the loaded paper."
                    : "If the printer rejects custom media, create/select the matching label size in Windows printer preferences and retry.";
                AppLogService.Error("[StickerPrinter] Print failed", ex);
                return new StickerPrintResult
                {
                    Message = "Sticker printing failed: " + ex.Message + "\n\n" + hint
                };
            }
        }

        public static StickerOutputLayoutMode ResolveOutputLayout(StickerPrintPreferences preferences)
        {
            if (preferences != null
                && Enum.IsDefined(typeof(StickerOutputLayoutMode), preferences.OutputLayoutMode)
                && preferences.OutputLayoutMode != StickerOutputLayoutMode.PrinterDefault)
                return preferences.OutputLayoutMode;
            return preferences?.PrinterModel == StickerPrinterModel.ZebraZt411
                ? StickerOutputLayoutMode.OneLabelPerPage
                : StickerOutputLayoutMode.A4Sheet;
        }

        private static bool TryCreateA4SheetPlan(
            PrintDialog dialog,
            StickerPrintPreferences preferences,
            StickerLayoutTemplate template,
            out PrintTicket ticket,
            out StickerSheetLayoutPlan plan,
            out bool usedConservativeMargins,
            out string error)
        {
            ticket = dialog.PrintTicket ?? new PrintTicket();
            plan = default;
            usedConservativeMargins = false;
            error = string.Empty;
            var queue = dialog.PrintQueue;
            if (queue == null)
            {
                error = "No printer queue was selected. Choose a standard printer and retry the A4 sheet layout.";
                return false;
            }

            bool reportedA4 = false;
            bool foundPlan = false;
            bool bestUsesFallbackMargins = false;
            string lastPlanError = string.Empty;
            string lastCapabilityError = string.Empty;
            foreach (var orientation in new[] { StickerSheetOrientation.Portrait, StickerSheetOrientation.Landscape })
            {
                double pageWidthMm = orientation == StickerSheetOrientation.Portrait
                    ? StickerSheetLayoutPlanner.A4WidthMm : StickerSheetLayoutPlanner.A4HeightMm;
                double pageHeightMm = orientation == StickerSheetOrientation.Portrait
                    ? StickerSheetLayoutPlanner.A4HeightMm : StickerSheetLayoutPlanner.A4WidthMm;

                ticket.PageMediaSize = new PageMediaSize(PageMediaSizeName.ISOA4);
                ticket.PageOrientation = orientation == StickerSheetOrientation.Portrait
                    ? PageOrientation.Portrait : PageOrientation.Landscape;
                PrintCapabilities capabilities;
                try
                {
                    capabilities = queue.GetPrintCapabilities(ticket);
                }
                catch (Exception ex)
                {
                    lastCapabilityError = ex.Message;
                    continue;
                }
                var supportedMediaSizes = capabilities.PageMediaSizeCapability;
                bool hasMediaList = supportedMediaSizes != null && supportedMediaSizes.Count > 0;
                bool supportsA4 = !hasMediaList || supportedMediaSizes!.Any(IsA4MediaSize);
                if (!supportsA4) continue;
                reportedA4 = true;

                var imageable = capabilities.PageImageableArea;
                bool useFallback = imageable == null
                    || !double.IsFinite(imageable.OriginWidth)
                    || !double.IsFinite(imageable.OriginHeight)
                    || !double.IsFinite(imageable.ExtentWidth)
                    || !double.IsFinite(imageable.ExtentHeight)
                    || imageable.ExtentWidth <= 0
                    || imageable.ExtentHeight <= 0;
                double leftMm;
                double topMm;
                double widthMm;
                double heightMm;
                if (!useFallback)
                {
                    leftMm = StickerLabelVisualFactory.DipToMm(imageable!.OriginWidth);
                    topMm = StickerLabelVisualFactory.DipToMm(imageable.OriginHeight);
                    widthMm = StickerLabelVisualFactory.DipToMm(imageable.ExtentWidth);
                    heightMm = StickerLabelVisualFactory.DipToMm(imageable.ExtentHeight);
                }
                else
                {
                    const double conservativeDriverMarginMm = 10;
                    leftMm = topMm = conservativeDriverMarginMm;
                    widthMm = Math.Max(0, pageWidthMm - 2 * conservativeDriverMarginMm);
                    heightMm = Math.Max(0, pageHeightMm - 2 * conservativeDriverMarginMm);
                }

                if (!StickerSheetLayoutPlanner.TryCreatePlan(
                        orientation, pageWidthMm, pageHeightMm,
                        leftMm, topMm, widthMm, heightMm,
                        template.LabelWidthMm, template.LabelHeightMm,
                        preferences.SheetMarginMm, preferences.SheetGapMm,
                        out var candidate, out lastPlanError))
                    continue;

                if (!foundPlan || candidate.LabelsPerSheet > plan.LabelsPerSheet
                    || (candidate.LabelsPerSheet == plan.LabelsPerSheet
                        && candidate.Orientation == StickerSheetOrientation.Portrait))
                {
                    plan = candidate;
                    foundPlan = true;
                    bestUsesFallbackMargins = useFallback;
                }
            }

            if (!reportedA4)
            {
                error = string.IsNullOrWhiteSpace(lastCapabilityError)
                    ? "The selected printer driver does not report A4 media. Choose a printer/driver with A4 support, or switch the studio to one sticker per page."
                    : "Could not read A4 paper capabilities from the selected printer driver: " + lastCapabilityError;
                return false;
            }
            if (!foundPlan)
            {
                error = string.IsNullOrWhiteSpace(lastPlanError)
                    ? "The sticker size does not fit in the selected printer's A4 printable area. Reduce its dimensions or use one sticker per page."
                    : lastPlanError;
                return false;
            }

            ticket.PageMediaSize = new PageMediaSize(PageMediaSizeName.ISOA4);
            ticket.PageOrientation = plan.Orientation == StickerSheetOrientation.Portrait
                ? PageOrientation.Portrait : PageOrientation.Landscape;
            usedConservativeMargins = bestUsesFallbackMargins;
            return true;
        }

        private static bool IsA4MediaSize(PageMediaSize media)
        {
            if (media.PageMediaSizeName == PageMediaSizeName.ISOA4) return true;
            if (!media.Width.HasValue || !media.Height.HasValue) return false;

            double expectedShort = StickerLabelVisualFactory.MmToDip(StickerSheetLayoutPlanner.A4WidthMm);
            double expectedLong = StickerLabelVisualFactory.MmToDip(StickerSheetLayoutPlanner.A4HeightMm);
            double width = media.Width.Value;
            double height = media.Height.Value;
            double tolerance = StickerLabelVisualFactory.MmToDip(2);
            return (Math.Abs(width - expectedShort) <= tolerance && Math.Abs(height - expectedLong) <= tolerance)
                || (Math.Abs(width - expectedLong) <= tolerance && Math.Abs(height - expectedShort) <= tolerance);
        }
    }
}
