namespace ProGlassAutomation.Services.Export.Pdf
{
    /// <summary>
    /// Live print tokens. Change the active <see cref="PdfTheme"/> and
    /// every module PDF (PI, JO, DN, TI, Statement, Sheet Store) follows.
    /// Printed documents stay light-mode — these are not UI theme brushes.
    /// </summary>
    public static class DocumentStyle
    {
        public static PdfTheme Current { get; private set; } =
            PdfThemeCatalog.Resolve(PdfThemeCatalog.ClassicNavy);

        public static void Apply(PdfTheme theme)
        {
            if (theme != null)
                Current = theme;
        }

        public static void Apply(string? layoutId) =>
            Apply(PdfThemeCatalog.Resolve(layoutId));

        // ── Fonts ─────────────────────────────────────────────────────
        public const string FontFamily = "Segoe UI";
        public const float FontSizeTitle = 20f;
        public const float FontSizeH1 = 14f;
        public const float FontSizeH2 = 11f;
        public const float FontSizeBody = 10f;
        public const float FontSizeSmall = 9f;
        public const float FontSizeCaption = 8f;

        // ── Colors (from the active print layout) ─────────────────────
        public static string ColorAccent => Current.Accent;
        public static string ColorAccentDark => Current.AccentDark;
        public static string ColorAccentLight => Current.AccentLight;
        public static string ColorAccentSoft => Current.AccentSoft;
        public static string ColorAccentOn => Current.AccentOn;
        public static string ColorText => Current.Text;
        public static string ColorMuted => Current.Muted;
        public static string ColorBorder => Current.Border;
        public static string ColorTableHeader => Current.TableHeader;
        public static string ColorTableAlt => Current.TableAlt;
        public static string ColorSurface => Current.Surface;
        public static string ColorDanger => Current.Danger;
        public static string ColorSuccess => Current.Success;
        public static string ColorWarning => Current.Warning;
        public static string ColorWhite => Current.White;

        // ── Spacing (points) ──────────────────────────────────────────
        public const float SpaceSection = 10f;
        public const float SpaceParagraph = 6f;
        public const float SpaceRow = 4f;

        public const float TableBorderThickness = 0.6f;
        public const float TableCellPaddingH = 5f;
        public const float TableCellPaddingV = 3.5f;
    }
}
