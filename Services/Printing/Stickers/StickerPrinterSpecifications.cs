using System;
using System.Globalization;

namespace ProGlassAutomation.Services.Printing.Stickers
{
    public enum StickerPrinterModel
    {
        Custom = 0,
        ZebraZt411 = 1
    }

    public enum StickerOutputLayoutMode
    {
        /// <summary>Use A4 multi-up for a standard/custom printer and one custom-media label for the ZT411 profile.</summary>
        PrinterDefault = 0,
        OneLabelPerPage = 1,
        A4Sheet = 2
    }

    public enum StickerPrinterResolution
    {
        Dpi203 = 203,
        Dpi300 = 300,
        Dpi600 = 600
    }

    public enum StickerMediaHandling
    {
        TearOff = 0,
        PeelOff = 1,
        Rewind = 2,
        Cutter = 3
    }

    public sealed class StickerPrinterOption<TValue> where TValue : struct, Enum
    {
        public StickerPrinterOption(TValue value, string displayName)
        {
            Value = value;
            DisplayName = displayName;
        }

        public TValue Value { get; }
        public string DisplayName { get; }
    }

    public readonly record struct StickerPrinterLimits(
        double MinimumLabelWidthMm,
        double MaximumPrintableWidthMm,
        double MinimumLabelLengthMm,
        double MaximumLabelLengthMm,
        double MinimumMediaWebWidthMm,
        double MaximumMediaWebWidthMm,
        string Description);

    /// <summary>
    /// Capability guardrails for custom sticker pages. ZT411 figures are based on
    /// Zebra's published media and print specifications; they do not configure the
    /// Windows printer driver or identify the hardware automatically.
    /// </summary>
    public static class StickerPrinterSpecifications
    {
        public const double Zt411MaximumPrintWidthMm = 104;
        public const double Zt411MinimumMediaWebWidthMm = 25.4;
        public const double Zt411MaximumTearOffOrCutterMediaWidthMm = 114;
        public const double Zt411MaximumPeelOrRewindMediaWidthMm = 108;
        public const double Zt411MinimumTearOffPeelOrRewindLengthMm = 12.7;
        public const double Zt411MinimumCutterLengthMm = 25.4;
        public const double Zt411MaximumLengthAt203Or300DpiMm = 991;
        public const double Zt411MaximumLengthAt600DpiMm = 508;
        public const double Zt411MaximumCutterCatchTrayLengthMm = 152.4;

        public const double GenericMinimumWidthMm = 15;
        public const double GenericMaximumWidthMm = 500;
        public const double GenericMinimumLengthMm = 15;
        public const double GenericMaximumLengthMm = 500;
        public const double MinimumTemplateLengthMm = 12.7;
        public const double MaximumTemplateLengthMm = 991;

        public static StickerPrinterLimits GetLimits(
            StickerPrinterModel model,
            StickerPrinterResolution resolution,
            StickerMediaHandling handling)
        {
            if (model != StickerPrinterModel.ZebraZt411)
            {
                return new StickerPrinterLimits(
                    GenericMinimumWidthMm,
                    GenericMaximumWidthMm,
                    GenericMinimumLengthMm,
                    GenericMaximumLengthMm,
                    GenericMinimumWidthMm,
                    GenericMaximumWidthMm,
                    "Custom printer: manual ranges are 15–500 mm width and 15–500 mm length. Confirm the actual limits in the printer driver before printing.");
            }

            bool cutter = handling == StickerMediaHandling.Cutter;
            bool peelOrRewind = handling is StickerMediaHandling.PeelOff or StickerMediaHandling.Rewind;
            double maximumMediaWidth = peelOrRewind
                ? Zt411MaximumPeelOrRewindMediaWidthMm
                : Zt411MaximumTearOffOrCutterMediaWidthMm;
            double minimumLength = cutter
                ? Zt411MinimumCutterLengthMm
                : Zt411MinimumTearOffPeelOrRewindLengthMm;
            double maximumLength = cutter
                ? Zt411MaximumCutterCatchTrayLengthMm
                : resolution == StickerPrinterResolution.Dpi600
                    ? Zt411MaximumLengthAt600DpiMm
                    : Zt411MaximumLengthAt203Or300DpiMm;
            string resolutionText = resolution == StickerPrinterResolution.Dpi600
                ? "600 dpi" : resolution == StickerPrinterResolution.Dpi300 ? "300 dpi" : "203 dpi";
            string handlingText = handling switch
            {
                StickerMediaHandling.PeelOff => "peel-off",
                StickerMediaHandling.Rewind => "rewind",
                StickerMediaHandling.Cutter => "cutter with catch tray",
                _ => "tear-off"
            };
            string lengthNote = cutter
                ? "cutter/catch-tray length 25.4–152.4 mm"
                : resolution == StickerPrinterResolution.Dpi600
                    ? "label length 12.7–508 mm at 600 dpi"
                    : "label length 12.7–991 mm at 203/300 dpi";
            return new StickerPrinterLimits(
                Zt411MinimumMediaWebWidthMm,
                Zt411MaximumPrintWidthMm,
                minimumLength,
                maximumLength,
                Zt411MinimumMediaWebWidthMm,
                maximumMediaWidth,
                "ZT411 non-RFID media, " + resolutionText + " / " + handlingText
                + ": printable layout width 25.4–104 mm; media web 25.4–" + maximumMediaWidth.ToString("0.#", CultureInfo.InvariantCulture)
                + " mm; " + lengthNote + ". Width above 104 mm is feedable stock but outside the 104 mm print width. Match DPI and handling mode to the Windows printer driver.");
        }

        public static bool TryValidateDimensions(
            StickerPrinterModel model,
            StickerPrinterResolution resolution,
            StickerMediaHandling handling,
            double widthMm,
            double lengthMm,
            out string error)
        {
            error = string.Empty;
            var limits = GetLimits(model, resolution, handling);
            if (!double.IsFinite(widthMm)
                || widthMm < limits.MinimumLabelWidthMm
                || widthMm > limits.MaximumPrintableWidthMm)
            {
                error = model == StickerPrinterModel.ZebraZt411
                    ? "For the selected ZT411 profile, printable sticker width must be between 25.4 and 104 mm. The printer can feed wider stock (up to 108 or 114 mm depending on handling), but its maximum print width is 104 mm."
                    : "Custom printer sticker width must be between 15 and 500 mm.";
                return false;
            }

            if (!double.IsFinite(lengthMm)
                || lengthMm < limits.MinimumLabelLengthMm
                || lengthMm > limits.MaximumLabelLengthMm)
            {
                error = "Sticker length must be between " + limits.MinimumLabelLengthMm.ToString("0.#")
                    + " and " + limits.MaximumLabelLengthMm.ToString("0.#") + " mm for the selected printer profile.";
                return false;
            }

            return true;
        }
    }
}
