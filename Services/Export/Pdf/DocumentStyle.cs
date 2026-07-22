namespace ProGlassAutomation.Services.Export.Pdf
{
    /// <summary>
    /// Central style tokens for all PDF documents.
    /// Change once here → applies to every module's PDF output.
    ///
    /// Colors are hex strings passed directly to QuestPDF (no theme
    /// awareness — printed documents are always light-mode).
    /// </summary>
    public static class DocumentStyle
    {
        // ── Fonts ─────────────────────────────────────────────────────
        public const string FontFamily = "Segoe UI";
        public const float FontSizeTitle = 20f;
        public const float FontSizeH1 = 14f;
        public const float FontSizeH2 = 12f;
        public const float FontSizeBody = 10f;
        public const float FontSizeSmall = 9f;
        public const float FontSizeCaption = 8f;

        // ── Colors ────────────────────────────────────────────────────
        public const string ColorAccent = "#0F4C3A";
        public const string ColorAccentLight = "#E8F5EE";
        public const string ColorText = "#111827";
        public const string ColorMuted = "#6B7280";
        public const string ColorBorder = "#E5E7EB";
        public const string ColorTableHeader = "#F3F4F6";
        public const string ColorTableAlt = "#FAFBFC";
        public const string ColorDanger = "#DC2626";
        public const string ColorSuccess = "#16A34A";
        public const string ColorWarning = "#F59E0B";
        public const string ColorWhite = "#FFFFFF";

        // ── Spacing (in points — 1pt = ~0.353mm) ──────────────────────
        public const float SpaceSection = 12f;
        public const float SpaceParagraph = 6f;
        public const float SpaceRow = 4f;

        // ── Table ─────────────────────────────────────────────────────
        public const float TableBorderThickness = 0.75f;
        public const float TableCellPaddingH = 4f;
        public const float TableCellPaddingV = 3f;
    }
}