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
                Description = "Formal letterhead: full navy masthead, two-column bill cards, solid table headers, boxed grand total and paired signature stamps. Default for customer invoices.",
                Kind = PdfLayoutKind.FormalBand,
                Accent = "#0B1F3A",
                AccentDark = "#061326",
                AccentLight = "#E8EEF5",
                AccentSoft = "#C5D0DE",
                AccentOn = "#FFFFFF",
                Text = "#111827",
                Muted = "#5B6573",
                Border = "#D5DBE3",
                TableHeader = "#0B1F3A",
                TableAlt = "#F5F7FA",
                Surface = "#F3F5F8",
                Danger = "#B91C1C",
                Success = "#047857",
                Warning = "#B45309",
                Highlight = "#C9A227",
                MarginMm = 16f,
                ContentSpacing = 10f,
                TitleSize = 15f,
                IdSize = 16f,
                TableFont = 8f,
                CellPadH = 5f,
                CellPadV = 3.5f,
                Zebra = true,
                TableGrid = false,
                RepeatTitleInBody = false
            },
            new PdfTheme
            {
                Id = ModernTeal,
                DisplayName = "Modern Teal",
                Description = "Split console: oversized title on the left, stacked meta card on the right, soft fill cards, light table header, open signature lines. Contemporary commercial invoice.",
                Kind = PdfLayoutKind.SplitConsole,
                Accent = "#0F766E",
                AccentDark = "#115E59",
                AccentLight = "#CCFBF1",
                AccentSoft = "#99F6E4",
                AccentOn = "#FFFFFF",
                Text = "#134E4A",
                Muted = "#5B716E",
                Border = "#D1E5E2",
                TableHeader = "#115E59",
                TableAlt = "#F0FDFA",
                Surface = "#F0FDFA",
                Danger = "#B91C1C",
                Success = "#047857",
                Warning = "#B45309",
                Highlight = "#0F766E",
                MarginMm = 15f,
                ContentSpacing = 12f,
                TitleSize = 22f,
                IdSize = 13f,
                TableFont = 8f,
                CellPadH = 6f,
                CellPadV = 4f,
                Zebra = true,
                TableGrid = false,
                RepeatTitleInBody = false
            },
            new PdfTheme
            {
                Id = EditorialInk,
                DisplayName = "Editorial Ink",
                Description = "Magazine spread: huge document title, numbered hairline sections, unboxed parties, letter-spaced table labels, typographic totals. Quiet hierarchy, lots of air.",
                Kind = PdfLayoutKind.Editorial,
                Accent = "#1C1917",
                AccentDark = "#0C0A09",
                AccentLight = "#F5F0EB",
                AccentSoft = "#E7D5C5",
                AccentOn = "#FAFAF9",
                Text = "#1C1917",
                Muted = "#78716C",
                Border = "#E7E5E4",
                TableHeader = "#1C1917",
                TableAlt = "#FAFAF9",
                Surface = "#F5F5F4",
                Danger = "#B91C1C",
                Success = "#3F6212",
                Warning = "#C2410C",
                Highlight = "#C2410C",
                MarginMm = 20f,
                ContentSpacing = 14f,
                TitleSize = 24f,
                IdSize = 11f,
                TableFont = 8f,
                CellPadH = 4f,
                CellPadV = 5f,
                Zebra = false,
                TableGrid = false,
                RepeatTitleInBody = false
            },
            new PdfTheme
            {
                Id = IndustrialSteel,
                DisplayName = "Industrial Steel",
                Description = "Workshop docket: full-bleed slate band, amber rail, KPI strip, dense grid tables, form-cell parties, stamp-box sign-off. Built for job orders and delivery notes.",
                Kind = PdfLayoutKind.Workshop,
                Accent = "#334155",
                AccentDark = "#1E293B",
                AccentLight = "#F1F5F9",
                AccentSoft = "#CBD5E1",
                AccentOn = "#FFFFFF",
                Text = "#0F172A",
                Muted = "#64748B",
                Border = "#94A3B8",
                TableHeader = "#1E293B",
                TableAlt = "#F8FAFC",
                Surface = "#F1F5F9",
                Danger = "#B91C1C",
                Success = "#047857",
                Warning = "#D97706",
                Highlight = "#D97706",
                MarginMm = 12f,
                ContentSpacing = 8f,
                TitleSize = 11f,
                IdSize = 18f,
                TableFont = 7.5f,
                CellPadH = 4f,
                CellPadV = 2.5f,
                Zebra = true,
                TableGrid = true,
                RepeatTitleInBody = true
            },
            new PdfTheme
            {
                Id = MinimalIvory,
                DisplayName = "Minimal Ivory",
                Description = "Quiet form: wide margins, no fill, hairline rules only, definition-list parties, rule-only totals, two signature lines. Maximum print contrast.",
                Kind = PdfLayoutKind.QuietForm,
                Accent = "#111827",
                AccentDark = "#030712",
                AccentLight = "#F9FAFB",
                AccentSoft = "#E5E7EB",
                AccentOn = "#FFFFFF",
                Text = "#111827",
                Muted = "#6B7280",
                Border = "#D1D5DB",
                TableHeader = "#111827",
                TableAlt = "#FFFFFF",
                Surface = "#FAFAF9",
                Danger = "#991B1B",
                Success = "#065F46",
                Warning = "#92400E",
                Highlight = "#111827",
                MarginMm = 22f,
                ContentSpacing = 16f,
                TitleSize = 10f,
                IdSize = 20f,
                TableFont = 8f,
                CellPadH = 3f,
                CellPadV = 4f,
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
            catch { /* config not ready */ }
            DocumentStyle.Apply(Resolve(id));
        }
    }
}
