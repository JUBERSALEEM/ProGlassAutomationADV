namespace ProGlassAutomation.Services.Theme.Engine
{
    /// <summary>Canonical visual families. Persisted engine strings still
    /// use VisualThemeEngine constants; this enum is the product contract.</summary>
    public enum ProGlassThemeFamily
    {
        Industrial,
        Monet,
        Crystal,
        /// <summary>Legacy iOS 26 family retained for config migration only;
        /// it is no longer one of the selectable eight theme families.</summary>
        Ios26,
        BentoGrid,
        SpatialImmersive,
        SwissInternational,
        HolographicIridescent,
        Neumorphism,
        Claymorphism,
        LiquidGlass,
        Glassmorphism
    }

    public enum ProGlassThemeMode
    {
        Light,
        Dark
    }

    public enum ProGlassMaterial
    {
        Solid,
        Soft,
        Frosted,
        Crystal
    }

    public enum ProGlassDensity
    {
        Compact,
        Normal,
        Comfortable,
        Spacious
    }
}
