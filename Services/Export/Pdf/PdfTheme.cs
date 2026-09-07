using System;
using System.Collections.Generic;

namespace ProGlassAutomation.Services.Export.Pdf
{
    /// <summary>
    /// How the page is composed — not a colour swap.
    /// Each kind changes letterhead, section chrome, tables, totals,
    /// signatures, margins and density independently of the palette.
    /// </summary>
    public enum PdfLayoutKind
    {
        FormalBand,
        SplitConsole,
        Editorial,
        Workshop,
        QuietForm
    }

    /// <summary>
    /// One professional print layout. Printed documents stay light-mode.
    /// Colours support the architecture; they do not define it.
    /// </summary>
    public sealed class PdfTheme
    {
        public string Id { get; init; } = "";
        public string DisplayName { get; init; } = "";
        public string Description { get; init; } = "";
        public PdfLayoutKind Kind { get; init; } = PdfLayoutKind.FormalBand;

        public string Accent { get; init; } = "#0B1F3A";
        public string AccentDark { get; init; } = "#061326";
        public string AccentLight { get; init; } = "#E8EEF5";
        public string AccentSoft { get; init; } = "#C5D0DE";
        public string AccentOn { get; init; } = "#FFFFFF";
        public string Text { get; init; } = "#111827";
        public string Muted { get; init; } = "#6B7280";
        public string Border { get; init; } = "#E5E7EB";
        public string TableHeader { get; init; } = "#0B1F3A";
        public string TableAlt { get; init; } = "#F8FAFC";
        public string Surface { get; init; } = "#F4F6F8";
        public string Danger { get; init; } = "#B91C1C";
        public string Success { get; init; } = "#047857";
        public string Warning { get; init; } = "#B45309";
        public string White { get; init; } = "#FFFFFF";
        public string Highlight { get; init; } = "#C9A227";

        public float MarginMm { get; init; } = 16f;
        public float ContentSpacing { get; init; } = 10f;
        public float TitleSize { get; init; } = 15f;
        public float IdSize { get; init; } = 16f;
        public float TableFont { get; init; } = 8f;
        public float CellPadH { get; init; } = 5f;
        public float CellPadV { get; init; } = 3.5f;
        public bool Zebra { get; init; } = true;
        public bool TableGrid { get; init; } = false;
        public bool RepeatTitleInBody { get; init; } = false;
    }

    public static class PdfThemeCatalog
    {
        public const string ClassicNavy = "ClassicNavy";
        public const string ModernTeal = "ModernTeal";
        public const string EditorialInk = "EditorialInk";
        public const string IndustrialSteel = "IndustrialSteel";
        public const string MinimalIvory = "MinimalIvory";

        public static readonly IReadOnlyList<PdfTheme> All = new[]
        {
            new PdfTheme
            {
                Id = ClassicNavy,
                DisplayName = "Classic Navy",
                Description = "Executive industrial letterhead: deep navy command band, brass approval accent, controlled ERP metrics, bordered commercial cards, disciplined totals and stamp-ready signatures.",
                Kind = PdfLayoutKind.FormalBand,
                Accent = "#0A2540",
                AccentDark = "#061728",
                AccentLight = "#E7EDF5",
                AccentSoft = "#BFD0E3",
                AccentOn = "#FFFFFF",
                Text = "#101827",
                Muted = "#526173",
                Border = "#C9D3DF",
                TableHeader = "#0A2540",
                TableAlt = "#F4F7FA",
                Surface = "#EEF3F8",
                Danger = "#B42318",
                Success = "#027A48",
                Warning = "#B54708",
                Highlight = "#D6A827",
                MarginMm = 15.5f,
                ContentSpacing = 9.5f,
                TitleSize = 15.5f,
                IdSize = 16.5f,
                TableFont = 8f,
                CellPadH = 5.2f,
                CellPadV = 3.4f,
                Zebra = true,
                TableGrid = false,
                RepeatTitleInBody = false
            },
            new PdfTheme
            {
                Id = ModernTeal,
                DisplayName = "Modern Teal",
                Description = "Modern factory console: large document command title, teal/cyan status rail, soft technical cards, clean metric strip, airy tables and fast management scanning.",
                Kind = PdfLayoutKind.SplitConsole,
                Accent = "#0E7490",
                AccentDark = "#164E63",
                AccentLight = "#CFFAFE",
                AccentSoft = "#A5F3FC",
                AccentOn = "#FFFFFF",
                Text = "#12343B",
                Muted = "#58717A",
                Border = "#BFE5EC",
                TableHeader = "#155E75",
                TableAlt = "#F0FDFF",
                Surface = "#ECFEFF",
                Danger = "#B42318",
                Success = "#067647",
                Warning = "#B54708",
                Highlight = "#06B6D4",
                MarginMm = 14.5f,
                ContentSpacing = 11f,
                TitleSize = 22.5f,
                IdSize = 13.5f,
                TableFont = 8f,
                CellPadH = 5.8f,
                CellPadV = 3.8f,
                Zebra = true,
                TableGrid = false,
                RepeatTitleInBody = false
            },
            new PdfTheme
            {
                Id = EditorialInk,
                DisplayName = "Editorial Ink",
                Description = "Premium board-report style: black-ink masthead, copper audit rail, numbered sections, typographic totals and high-end whitespace for director/client packs.",
                Kind = PdfLayoutKind.Editorial,
                Accent = "#18181B",
                AccentDark = "#09090B",
                AccentLight = "#F5F0EA",
                AccentSoft = "#E6D3C1",
                AccentOn = "#FAFAFA",
                Text = "#18181B",
                Muted = "#716B65",
                Border = "#DDD6CE",
                TableHeader = "#18181B",
                TableAlt = "#FBFAF8",
                Surface = "#F6F2EC",
                Danger = "#A61B1B",
                Success = "#3F6212",
                Warning = "#B45309",
                Highlight = "#B85C38",
                MarginMm = 19f,
                ContentSpacing = 13f,
                TitleSize = 24.5f,
                IdSize = 11.5f,
                TableFont = 8f,
                CellPadH = 4.2f,
                CellPadV = 4.6f,
                Zebra = false,
                TableGrid = false,
                RepeatTitleInBody = false
            },
            new PdfTheme
            {
                Id = IndustrialSteel,
                DisplayName = "Industrial Steel",
                Description = "Heavy industrial steel docket: graphite header, safety-amber rail, dense QA grid, high-contrast totals and stamp-box sign-off for production and dispatch floors.",
                Kind = PdfLayoutKind.Workshop,
                Accent = "#2F3A46",
                AccentDark = "#111827",
                AccentLight = "#E8EDF2",
                AccentSoft = "#CBD5E1",
                AccentOn = "#FFFFFF",
                Text = "#0B1220",
                Muted = "#56677C",
                Border = "#8998AA",
                TableHeader = "#111827",
                TableAlt = "#F3F6F9",
                Surface = "#E9EEF4",
                Danger = "#B42318",
                Success = "#027A48",
                Warning = "#D97706",
                Highlight = "#F59E0B",
                MarginMm = 11.5f,
                ContentSpacing = 7.5f,
                TitleSize = 11.2f,
                IdSize = 18.5f,
                TableFont = 7.4f,
                CellPadH = 3.8f,
                CellPadV = 2.4f,
                Zebra = true,
                TableGrid = true,
                RepeatTitleInBody = true
            },
            new PdfTheme
            {
                Id = MinimalIvory,
                DisplayName = "Minimal Ivory",
                Description = "Minimal ivory executive form: warm paper tone, sharp black rules, silent ERP metrics, refined amount emphasis and clean archive-grade print contrast.",
                Kind = PdfLayoutKind.QuietForm,
                Accent = "#1F2937",
                AccentDark = "#030712",
                AccentLight = "#FAF7F0",
                AccentSoft = "#E7DED1",
                AccentOn = "#FFFFFF",
                Text = "#111827",
                Muted = "#6F665C",
                Border = "#D9D0C3",
                TableHeader = "#1F2937",
                TableAlt = "#FFFFFF",
                Surface = "#FBF7EF",
                Danger = "#991B1B",
                Success = "#065F46",
                Warning = "#92400E",
                Highlight = "#8B5E34",
                MarginMm = 21f,
                ContentSpacing = 15f,
                TitleSize = 10.5f,
                IdSize = 20.5f,
                TableFont = 8f,
                CellPadH = 3.2f,
                CellPadV = 3.9f,
                Zebra = false,
                TableGrid = false,
                RepeatTitleInBody = false
            }
        };

        public static PdfTheme Resolve(string? id)
        {
            if (string.IsNullOrWhiteSpace(id))
                return All[0];
            foreach (var theme in All)
            {
                if (string.Equals(theme.Id, id, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(theme.DisplayName, id, StringComparison.OrdinalIgnoreCase))
                    return theme;
            }
            return All[0];
        }

        public static void ApplySaved()
        {
            string? id = null;
            try { id = App.Config?.Appearance?.PdfLayoutId; }
            catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[PdfTheme] ApplySaved: config not ready: " + ex.Message); }
            DocumentStyle.Apply(Resolve(id));
        }
    }
}
