using System.Windows.Media;

namespace ProGlassAutomation.Services.Theme.Engine
{
    /// <summary>Fallback TokenStore colors when a family painter has not
    /// published a source brush. Monet/Crystal live frost is never replaced
    /// when the source brush already exists.</summary>
    public static class ProGlassPalettes
    {
        public readonly struct Palette
        {
            public Palette(Color window, Color surface, Color elevated, Color input,
                Color text, Color textMuted, Color border, Color accent)
            {
                Window = window; Surface = surface; Elevated = elevated; Input = input;
                Text = text; TextMuted = textMuted; Border = border; Accent = accent;
            }

            public Color Window { get; }
            public Color Surface { get; }
            public Color Elevated { get; }
            public Color Input { get; }
            public Color Text { get; }
            public Color TextMuted { get; }
            public Color Border { get; }
            public Color Accent { get; }
        }

        public static Palette Resolve(ProGlassThemeFamily family, ProGlassThemeMode mode)
        {
            bool dark = mode == ProGlassThemeMode.Dark;
            return family switch
            {
                ProGlassThemeFamily.Monet => dark
                    ? new Palette(
                        C(0x0F, 0x17, 0x2A), C(0x16, 0x22, 0x38), C(0x1E, 0x29, 0x3B), C(0x16, 0x22, 0x38),
                        C(0xF1, 0xF5, 0xF9), C(0x94, 0xA3, 0xB8), C(0x47, 0x55, 0x69), C(0x03, 0x69, 0xA1))
                    : new Palette(
                        C(0xF8, 0xFA, 0xFC), C(0xFF, 0xFF, 0xFF), C(0xFF, 0xFF, 0xFF), C(0xFF, 0xFF, 0xFF),
                        C(0x0F, 0x17, 0x2A), C(0x47, 0x55, 0x69), C(0xCB, 0xD5, 0xE1), C(0x03, 0x69, 0xA1)),
                ProGlassThemeFamily.Crystal => dark
                    ? new Palette(
                        C(0x0E, 0x22, 0x30), C(0x12, 0x2A, 0x3A), C(0x16, 0x32, 0x44), C(0x12, 0x2A, 0x3A),
                        C(0xF2, 0xFA, 0xFE), C(0x7F, 0xA4, 0xBD), C(0x8F, 0xFF, 0xFF), C(0x55, 0xC9, 0xEE))
                    : new Palette(
                        C(0xEE, 0xF6, 0xFF), C(0xF8, 0xFC, 0xFF), C(0xFF, 0xFF, 0xFF), C(0xF8, 0xFC, 0xFF),
                        C(0x10, 0x29, 0x38), C(0x68, 0x80, 0x8F), C(0xC6, 0xE4, 0xFF), C(0x55, 0xC9, 0xEE)),
                _ => dark
                    ? new Palette(
                        C(0x0A, 0x13, 0x26), C(0x0F, 0x1D, 0x33), C(0x1E, 0x29, 0x3B), C(0x0F, 0x1D, 0x33),
                        C(0xF8, 0xFA, 0xFC), C(0x94, 0xA3, 0xB8), C(0x47, 0x55, 0x69), C(0x03, 0x69, 0xA1))
                    : new Palette(
                        C(0xF8, 0xFA, 0xFC), C(0xFF, 0xFF, 0xFF), C(0xFF, 0xFF, 0xFF), C(0xFF, 0xFF, 0xFF),
                        C(0x0F, 0x17, 0x2A), C(0x47, 0x55, 0x69), C(0xCB, 0xD5, 0xE1), C(0x03, 0x69, 0xA1)),
            };
        }

        private static Color C(byte r, byte g, byte b) => Color.FromRgb(r, g, b);
    }
}
