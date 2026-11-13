using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;

namespace ProGlassAutomation.Customization;

/// <summary>
/// Shared type overlay. Writes FontFamily / weight / style / underline into the
/// active theme package so every theme consumes the same choice.
/// Not a material engine.
/// </summary>
public sealed record FontCustomizationState(
    string Family = "Segoe UI",
    bool HeadingBold = true,
    bool BodyItalic = false,
    bool BodyUnderline = false)
{
    public string? Validate()
    {
        if (string.IsNullOrWhiteSpace(Family) || Family.Length > 80)
            return "Font family is required.";
        return null;
    }
}

public static class FontService
{
    public const long MaxBytes = 8 * 1024 * 1024;
    public static readonly IReadOnlyList<string> BuiltIn =
    [
        "Segoe UI",
        "Segoe UI Variable",
        "Calibri",
        "Georgia",
        "Consolas",
        "Courier New",
    ];

    public static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".ttf", ".otf", ".ttc", ".otc"
    };

    public static string ImportDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "ProGlassAutomation", "fonts");

    public static IReadOnlyList<string> AvailableFamilies()
    {
        var names = new List<string>(BuiltIn);
        foreach (string imported in ListImported())
            if (!names.Contains(imported, StringComparer.OrdinalIgnoreCase))
                names.Add(imported);
        return names;
    }

    public static IReadOnlyList<string> ListImported()
    {
        if (!Directory.Exists(ImportDirectory)) return [];
        var found = new List<string>();
        foreach (string file in Directory.EnumerateFiles(ImportDirectory))
        {
            if (!AllowedExtensions.Contains(Path.GetExtension(file))) continue;
            try
            {
                string name = FamilyNameOf(file);
                if (!string.IsNullOrWhiteSpace(name) && !found.Contains(name, StringComparer.OrdinalIgnoreCase))
                    found.Add(name);
            }
            catch (Exception ex) when (ex is IOException or ArgumentException or NotSupportedException or FileFormatException)
            {
            }
        }
        return found;
    }

    public static string Import(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var info = new FileInfo(path);
        if (!info.Exists) throw new FileNotFoundException("Font file not found.", path);
        if (!AllowedExtensions.Contains(info.Extension))
            throw new ArgumentException("Use a .ttf, .otf, .ttc or .otc font file.");
        if (info.Length > MaxBytes) throw new ArgumentException("Font files must be at most 8 MB.");
        Directory.CreateDirectory(ImportDirectory);
        string dest = Path.Combine(ImportDirectory, info.Name);
        File.Copy(info.FullName, dest, overwrite: true);
        string name;
        try
        {
            name = FamilyNameOf(dest);
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or NotSupportedException or FileFormatException)
        {
            try { File.Delete(dest); } catch (IOException) { }
            throw new ArgumentException("The file is not a readable OpenType or TrueType font.");
        }
        if (string.IsNullOrWhiteSpace(name))
        {
            try { File.Delete(dest); } catch (IOException) { }
            throw new ArgumentException("The file is not a readable OpenType or TrueType font.");
        }
        return name;
    }

    public static void Apply(ResourceDictionary package, FontCustomizationState state)
    {
        ArgumentNullException.ThrowIfNull(package);
        ArgumentNullException.ThrowIfNull(state);
        if (state.Validate() is not null) throw new ArgumentException(state.Validate());
        FontFamily family = Resolve(state.Family);
        Set(package, "FontFamilyBody", family);
        if (BuiltInContains(state.Family) && IsMono(state.Family))
            Set(package, "FontFamilyMono", family);
        Set(package, "FontWeightHeading", state.HeadingBold ? FontWeights.Bold : FontWeights.SemiBold);
        Set(package, "FontWeightBody", FontWeights.Normal);
        Set(package, "FontStyleBody", state.BodyItalic ? FontStyles.Italic : FontStyles.Normal);
        var decorations = state.BodyUnderline
            ? (TextDecorationCollection)TextDecorations.Underline.Clone()
            : new TextDecorationCollection();
        if (decorations.CanFreeze) decorations.Freeze();
        Set(package, "TextDecorationsBody", decorations);
    }

    public static FontFamily Resolve(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return new FontFamily("Segoe UI");
        try
        {
            if (Directory.Exists(ImportDirectory))
            {
                foreach (string file in Directory.EnumerateFiles(ImportDirectory))
                {
                    if (!AllowedExtensions.Contains(Path.GetExtension(file))) continue;
                    try
                    {
                        if (!string.Equals(FamilyNameOf(file), name, StringComparison.OrdinalIgnoreCase)) continue;
                        string folder = ImportDirectory.EndsWith(Path.DirectorySeparatorChar)
                            ? ImportDirectory
                            : ImportDirectory + Path.DirectorySeparatorChar;
                        return new FontFamily(new Uri(folder), "./#" + name);
                    }
                    catch (Exception ex) when (ex is IOException or ArgumentException or NotSupportedException or FileFormatException)
                    {
                    }
                }
            }
            return new FontFamily(name);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException or FileFormatException)
        {
            return new FontFamily("Segoe UI");
        }
    }

    private static string FamilyNameOf(string file)
    {
        var typeface = new GlyphTypeface(new Uri(file, UriKind.Absolute));
        if (typeface.FamilyNames.TryGetValue(CultureInfo.CurrentCulture, out string? localized) && !string.IsNullOrWhiteSpace(localized))
            return localized.Trim();
        if (typeface.FamilyNames.TryGetValue(CultureInfo.InvariantCulture, out string? invariant) && !string.IsNullOrWhiteSpace(invariant))
            return invariant.Trim();
        foreach (string value in typeface.FamilyNames.Values)
            if (!string.IsNullOrWhiteSpace(value)) return value.Trim();
        return Path.GetFileNameWithoutExtension(file);
    }

    private static bool BuiltInContains(string name) =>
        BuiltIn.Any(item => string.Equals(item, name, StringComparison.OrdinalIgnoreCase));

    private static bool IsMono(string name) =>
        name.Contains("Consolas", StringComparison.OrdinalIgnoreCase)
        || name.Contains("Courier", StringComparison.OrdinalIgnoreCase);

    private static void Set(ResourceDictionary package, string key, object value)
    {
        if (Replace(package, key, value)) return;
        package[key] = value;
    }

    private static bool Replace(ResourceDictionary dictionary, string key, object value)
    {
        foreach (object existing in dictionary.Keys)
        {
            if (!Equals(existing, key)) continue;
            dictionary[key] = value;
            return true;
        }
        foreach (ResourceDictionary child in dictionary.MergedDictionaries)
            if (Replace(child, key, value)) return true;
        return false;
    }
}
