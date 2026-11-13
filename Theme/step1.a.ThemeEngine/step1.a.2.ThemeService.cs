using System.ComponentModel;
using System.IO;
using System.Windows.Markup;
using System.Windows;

namespace ProGlassAutomation.Theming;

public enum Appearance { Light, Dark }

/// <summary>One complete theme-package slot; loading/validation precede replacement.</summary>
public sealed class ThemeService : INotifyPropertyChanged
{
    public const string Bento = ThemeCatalog.BentoName;
    public const string Clay = ThemeCatalog.ClayName;
    public const string Fluent = ThemeCatalog.FluentName;
    public const string Neumorphism = ThemeCatalog.NeumorphismName;
    public const string LiquidGlass = ThemeCatalog.LiquidGlassName;
    public const string Glassmorphism = ThemeCatalog.GlassName;
    public const string Spatial = ThemeCatalog.SpatialName;
    public const string Swiss = ThemeCatalog.SwissName;
    public const string Holographic = ThemeCatalog.HolographicName;
    public static IReadOnlyList<string> InstalledThemes { get; } = Array.AsReadOnly(ThemeCatalog.Installed.Select(theme => theme.Name).ToArray());
    public Appearance ActiveAppearance { get; private set; } = Appearance.Light;
    public string ActiveTheme { get; private set; } = Bento;
    public event PropertyChangedEventHandler? PropertyChanged;
    /// <summary>Raised after the active package has been atomically selected or overlaid.</summary>
    public event EventHandler? PackageChanged;
    private ResourceDictionary? applicationResources;
    private ResourceDictionary? activeResources;
    private Action<ResourceDictionary, Appearance>? activeConfiguration;
    private static readonly Dictionary<(string Theme, Appearance Mode), ResourceDictionary> packed = new();

    public void Initialize(ResourceDictionary resources) =>
        Initialize(resources, Bento, Appearance.Light);

    /// <summary>Stage and validate the requested initial package before adding the slot.</summary>
    public void Initialize(ResourceDictionary resources, string theme, Appearance appearance,
        Action<ResourceDictionary, Appearance>? configure = null)
    {
        Application.Current.Dispatcher.VerifyAccess();
        ArgumentNullException.ThrowIfNull(resources);
        if (applicationResources is not null)
            throw new InvalidOperationException("The theme engine is already initialized.");
        // Load/validate before mutating the application resource graph. If this
        // fails, the host keeps its old theme with no half-installed package.
        var package = Load(appearance, theme, configure);
        resources.MergedDictionaries.Add(package);
        applicationResources = resources;
        activeResources = package;
        activeConfiguration = configure;
        ActiveTheme = theme;
        ActiveAppearance = appearance;
    }

    public void Apply(string theme, Appearance appearance, Action<ResourceDictionary, Appearance>? configure = null)
    {
        Application.Current.Dispatcher.VerifyAccess();
        if (!Enum.IsDefined(appearance))
            throw new ArgumentException("Only defined appearance modes can be selected.");
        if (applicationResources is null || activeResources is null)
            throw new InvalidOperationException("Initialize the theme engine before selecting an appearance.");
        var configuration = configure ?? (theme == ActiveTheme ? activeConfiguration : null);
        var candidate = Load(appearance, theme, configuration);
        int slot = applicationResources.MergedDictionaries.IndexOf(activeResources);
        if (slot < 0)
            throw new InvalidOperationException("The active theme resource slot was removed.");
        try { applicationResources.MergedDictionaries[slot] = candidate; }
        catch (Exception error) when (error is InvalidOperationException or ArgumentException or XamlParseException)
        {
            applicationResources.MergedDictionaries[slot] = activeResources;
            throw new InvalidOperationException($"Could not activate {theme} / {appearance}; previous package restored.", error);
        }
        activeResources = candidate;
        activeConfiguration = configuration;
        ActiveTheme = theme;
        ActiveAppearance = appearance;
        PackageChanged?.Invoke(this, EventArgs.Empty);
        var failures = new List<Exception>();
        foreach (string property in new[] { nameof(ActiveAppearance), nameof(ActiveTheme) })
            foreach (PropertyChangedEventHandler subscriber in PropertyChanged?.GetInvocationList() ?? Array.Empty<Delegate>())
                try { subscriber(this, new PropertyChangedEventArgs(property)); }
                catch (Exception error) { failures.Add(error); }
        if (failures.Count > 0)
            throw new ThemeNotificationException($"{theme} / {appearance} is active, but a consumer failed to refresh.", new AggregateException(failures));
    }

    /// <summary>
    /// Writes into the live package without replacing it. Font/color overlays must
    /// not swap dictionaries while a ListBox/ComboBox selection is running.
    /// </summary>
    public void Overlay(Action<ResourceDictionary, Appearance> mutate)
    {
        Application.Current.Dispatcher.VerifyAccess();
        ArgumentNullException.ThrowIfNull(mutate);
        if (activeResources is null)
            throw new InvalidOperationException("Initialize the theme engine before overlay.");
        mutate(activeResources, ActiveAppearance);
        ResourceValidation.Validate(activeResources, ActiveTheme + " / " + ActiveAppearance);
        PackageChanged?.Invoke(this, EventArgs.Empty);
    }

    public object GetToken(string key) => activeResources?[key]
        ?? throw new InvalidOperationException($"No active token named {key}.");

    /// <summary>Returns an active package resource by string or WPF type key.</summary>
    public object GetResource(object key) => activeResources?[key]
        ?? throw new InvalidOperationException($"No active resource named {key}.");

    public static ResourceDictionary Load(Appearance appearance, string theme = Bento, Action<ResourceDictionary, Appearance>? configure = null)
    {
        if (!Enum.IsDefined(appearance)) throw new ArgumentOutOfRangeException(nameof(appearance));
        var definition = ThemeCatalog.Find(theme);
        try
        {
            var resources = ClonePack(BasePackage(definition, appearance));
            configure?.Invoke(resources, appearance);
            ResourceValidation.Validate(resources, definition.Name + " / " + appearance);
            return resources;
        }
        catch (Exception error) when (error is IOException or XamlParseException or InvalidOperationException or ArgumentException)
        {
            throw new InvalidOperationException($"Cannot load {definition.Name} / {appearance}: {error.Message}", error);
        }
    }

    private static ResourceDictionary BasePackage(ThemeDefinition definition, Appearance appearance)
    {
        var key = (definition.Name, appearance);
        lock (packed)
        {
            if (!packed.TryGetValue(key, out var found))
            {
                found = definition.LoadResources(appearance)
                    ?? throw new InvalidOperationException("The theme returned no resource package.");
                packed[key] = found;
            }
            return found;
        }
    }

    private static ResourceDictionary ClonePack(ResourceDictionary source)
    {
        var copy = new ResourceDictionary();
        foreach (object key in source.Keys)
            copy[key] = source[key];
        foreach (ResourceDictionary child in source.MergedDictionaries)
            copy.MergedDictionaries.Add(ClonePack(child));
        return copy;
    }
}
