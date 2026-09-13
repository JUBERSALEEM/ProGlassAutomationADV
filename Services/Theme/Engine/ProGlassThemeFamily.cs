namespace ProGlassAutomation.Services.Theme.Engine
{
    /// <summary>Canonical visual families. Persisted engine strings still
    /// use VisualThemeEngine constants; this enum is the product contract.</summary>
    public enum ProGlassThemeFamily
    {
        Industrial,
        Monet,
        Crystal
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
