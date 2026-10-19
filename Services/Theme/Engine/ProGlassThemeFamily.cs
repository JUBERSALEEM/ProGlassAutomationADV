namespace ProGlassAutomation.Services.Theme.Engine
{
    /// <summary>Canonical visual families. Persisted engine strings still
    /// use VisualThemeEngine constants; this enum is the product contract.</summary>
    public enum ProGlassThemeFamily
    {
        Industrial,
        Monet,
        Crystal,
        /// <summary>iOS 26 liquid glass — fourth family (was missing from
        /// the contract, which is why the Settings picker offered only two
        /// themes; the engine override layer always existed).</summary>
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
