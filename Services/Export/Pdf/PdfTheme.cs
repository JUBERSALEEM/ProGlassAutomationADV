using System;
using System.Collections.Generic;

namespace ProGlassAutomation.Services.Export.Pdf
{
    /// <summary>
    /// One professional print palette. Printed documents stay light-mode;
    /// these tokens only change accent, table chrome and hierarchy.
    /// </summary>
    public sealed class PdfTheme
    {
        public string Id { get; init; } = "";
        public string DisplayName { get; init; } = "";
        public string Description { get; init; } = "";

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
                Description = "Corporate navy letterhead, gold-quiet tables. Default for customer invoices.",
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
                Warning = "#B45309"
            },
            new PdfTheme
            {
                Id = ModernTeal,
                DisplayName = "Modern Teal",
                Description = "Clean teal accent for a contemporary commercial invoice.",
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
                Warning = "#B45309"
            },
            new PdfTheme
            {
                Id = EditorialInk,
                DisplayName = "Editorial Ink",
                Description = "Charcoal and terracotta. Quiet, magazine-like hierarchy.",
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
                Warning = "#C2410C"
            },
            new PdfTheme
            {
                Id = IndustrialSteel,
                DisplayName = "Industrial Steel",
                Description = "Slate and amber. Best for job orders and delivery notes.",
                Accent = "#334155",
                AccentDark = "#1E293B",
                AccentLight = "#F1F5F9",
                AccentSoft = "#CBD5E1",
                AccentOn = "#FFFFFF",
                Text = "#0F172A",
                Muted = "#64748B",
                Border = "#E2E8F0",
                TableHeader = "#1E293B",
                TableAlt = "#F8FAFC",
                Surface = "#F1F5F9",
                Danger = "#B91C1C",
                Success = "#047857",
                Warning = "#D97706"
            },
            new PdfTheme
            {
                Id = MinimalIvory,
                DisplayName = "Minimal Ivory",
                Description = "Hairline rules, almost no fill. Maximum print contrast.",
                Accent = "#111827",
                AccentDark = "#030712",
                AccentLight = "#F9FAFB",
                AccentSoft = "#E5E7EB",
                AccentOn = "#FFFFFF",
                Text = "#111827",
                Muted = "#6B7280",
                Border = "#E5E7EB",
                TableHeader = "#111827",
                TableAlt = "#FFFFFF",
                Surface = "#FAFAF9",
                Danger = "#991B1B",
                Success = "#065F46",
                Warning = "#92400E"
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
