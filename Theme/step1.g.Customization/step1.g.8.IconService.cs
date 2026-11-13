using System.Windows;
using System.Windows.Media;

namespace ProGlassAutomation.Customization;

public enum IconKind
{
    Home, Theme, Texts, Colors, About, Marks,
    Appearance, Layout, Charts, Work, Settings, Author, Engineering, Components,
    Search, Add, Edit, Delete, Save, Document, Folder, Close, Menu, ArrowRight,
    Refresh, Print, Filter, More, Check, Warning, Calendar, Clock, Export, Import, Help
}

public enum IconMarkStyle
{
    FluentOutline,
    FluentFilled,
    MdlFilled,
    LucideThin,
    MaterialRound,
    Duotone,
    SoftPlate,
    Contrast,
    Compact,
    LineHeavy
}

public sealed record IconCustomizationState(IconMarkStyle Style = IconMarkStyle.FluentOutline)
{
    public string? Validate() => Enum.IsDefined(Style) ? null : "Unsupported icon style.";
}

public sealed record IconStyleCard(IconMarkStyle Style, string Name, string Caption);

/// <summary>
/// Shared icon overlay. Marks are native Path geometry, not PNG and not a tenth theme.
/// Pages pick a mark by Kind, by heading Title, or by Category.
/// </summary>
public static class IconService
{
    public static IconCustomizationState Active { get; private set; } = new();
    public static event EventHandler? Changed;

    public static readonly IReadOnlyList<IconStyleCard> Cards =
    [
        new(IconMarkStyle.FluentOutline, "Fluent outline", "Windows 11 line marks. Default."),
        new(IconMarkStyle.FluentFilled, "Fluent filled", "Solid glyphs, stronger at a glance."),
        new(IconMarkStyle.MdlFilled, "MDL2 filled", "Squarer Windows 10 blocks."),
        new(IconMarkStyle.LucideThin, "Lucide thin", "Editorial 1px stroke."),
        new(IconMarkStyle.MaterialRound, "Material round", "Filled marks on a soft plate."),
        new(IconMarkStyle.Duotone, "Duotone", "Tinted fill plus a crisp outline."),
        new(IconMarkStyle.SoftPlate, "Soft plate", "Outline sitting in a circular chip."),
        new(IconMarkStyle.Contrast, "Contrast", "Larger solid marks."),
        new(IconMarkStyle.Compact, "Compact", "Smaller outline for dense nav."),
        new(IconMarkStyle.LineHeavy, "Line heavy", "Bold stroke, still outline.")
    ];

    public static void Apply(IconCustomizationState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Validate() is not null) throw new ArgumentException(state.Validate());
        Active = state;
        Changed?.Invoke(null, EventArgs.Empty);
    }

    public static IconKind Resolve(string? name, string? category = null, IconKind fallback = IconKind.Home)
    {
        IconKind fromName = Match(name);
        if (fromName != IconKind.Home || LooksLikeHome(name)) return fromName;
        IconKind fromCategory = Match(category);
        if (fromCategory != IconKind.Home || LooksLikeHome(category)) return fromCategory;
        return fallback;
    }

    private static bool LooksLikeHome(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        string key = Normalize(text);
        return key is "home" or "business" or "focus";
    }

    private static IconKind Match(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return IconKind.Home;
        string key = Normalize(text);
        if (HasAnyWord(key, "search", "find", "lookup")) return IconKind.Search;
        if (HasAnyWord(key, "add", "create", "new")) return IconKind.Add;
        if (HasAnyWord(key, "edit", "modify", "pencil")) return IconKind.Edit;
        if (HasAnyWord(key, "delete", "remove", "trash")) return IconKind.Delete;
        if (HasAnyWord(key, "save", "apply")) return IconKind.Save;
        if (HasAnyWord(key, "document", "file", "page")) return IconKind.Document;
        if (HasAnyWord(key, "folder", "directory")) return IconKind.Folder;
        if (HasAnyWord(key, "close", "cancel", "dismiss")) return IconKind.Close;
        if (HasAnyWord(key, "menu", "navigation")) return IconKind.Menu;
        if (HasAnyWord(key, "arrow", "next", "forward")) return IconKind.ArrowRight;
        if (HasAnyWord(key, "refresh", "reload", "sync")) return IconKind.Refresh;
        if (HasAnyWord(key, "print")) return IconKind.Print;
        if (HasAnyWord(key, "filter")) return IconKind.Filter;
        if (HasAnyWord(key, "more", "overflow", "ellipsis")) return IconKind.More;
        if (HasAnyWord(key, "check", "done", "success")) return IconKind.Check;
        if (HasAnyWord(key, "warning", "alert")) return IconKind.Warning;
        if (HasAnyWord(key, "calendar", "date")) return IconKind.Calendar;
        if (HasAnyWord(key, "clock", "time")) return IconKind.Clock;
        if (HasAnyWord(key, "export", "download")) return IconKind.Export;
        if (HasAnyWord(key, "import", "upload")) return IconKind.Import;
        if (HasAnyWord(key, "help", "question")) return IconKind.Help;
        if (Contains(key, "dashboard", "home")) return IconKind.Home;
        if (Contains(key, "sheet", "store", "sales", "order", "delivery", "inventory")) return IconKind.Work;
        if (Contains(key, "specification", "component", "control")) return IconKind.Components;
        if (Contains(key, "production", "engineering")) return IconKind.Engineering;
        if (Contains(key, "report", "chart", "analytic")) return IconKind.Charts;
        if (Contains(key, "user", "author", "people")) return IconKind.Author;
        if (Contains(key, "icon", "mark")) return IconKind.Marks;
        if (Contains(key, "text", "type", "font", "typography")) return IconKind.Texts;
        if (Contains(key, "color", "palette", "wash", "accent")) return IconKind.Colors;
        if (Contains(key, "about", "info", "credit")) return IconKind.About;
        if (Contains(key, "author", "person", "people", "juber")) return IconKind.Author;
        if (Contains(key, "engineer", "notes", "wrench")) return IconKind.Engineering;
        if (Contains(key, "chart", "analytic", "revenue", "mix", "channel", "capacity", "conversion", "retention")) return IconKind.Charts;
        if (Contains(key, "layout", "preset", "density", "spacing")) return IconKind.Layout;
        if (Contains(key, "appearance", "canvas", "surface", "light", "dark", "pulse")) return IconKind.Appearance;
        if (Contains(key, "component", "control", "review")) return IconKind.Components;
        if (Contains(key, "setting", "customize", "design", "save", "reset", "import", "export")) return IconKind.Settings;
        if (Contains(key, "work", "queue", "activity", "recent", "job", "token", "surface")) return IconKind.Work;
        if (Contains(key, "theme", "studio", "engine", "bento", "clay", "fluent", "neu", "liquid", "glass", "spatial", "swiss", "holo", "presentation")) return IconKind.Theme;
        if (Contains(key, "home", "business", "focus")) return IconKind.Home;
        return IconKind.Home;
    }

    private static string Normalize(string text) =>
        string.Join(' ', text.ToLowerInvariant().Split(new[] { ' ', '/', '-', '_', ',', '.', '·' }, StringSplitOptions.RemoveEmptyEntries));

    private static bool HasAnyWord(string key, params string[] words)
    {
        string[] parts = key.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Any(part => words.Contains(part, StringComparer.Ordinal));
    }

    private static bool Contains(string key, params string[] tokens)
    {
        foreach (string token in tokens)
            if (key.Contains(token, StringComparison.Ordinal)) return true;
        return false;
    }

    public static IconRecipe Recipe(IconMarkStyle style) => style switch
    {
        IconMarkStyle.FluentFilled => new(true, 0, false, false, 1),
        IconMarkStyle.MdlFilled => new(true, 0, false, false, 1),
        IconMarkStyle.LucideThin => new(false, 1.15, false, false, 1),
        IconMarkStyle.MaterialRound => new(true, 0, false, true, 0.86),
        IconMarkStyle.Duotone => new(true, 1.45, true, false, 1),
        IconMarkStyle.SoftPlate => new(false, 1.5, false, true, 0.78),
        IconMarkStyle.Contrast => new(true, 0, false, false, 1.12),
        IconMarkStyle.Compact => new(false, 1.4, false, false, 0.78),
        IconMarkStyle.LineHeavy => new(false, 2.45, false, false, 0.96),
        _ => new(false, 1.6, false, false, 1)
    };

    public static Geometry Mark(IconKind kind, IconMarkStyle style)
    {
        bool filled = Recipe(style).Filled;
        bool mdl = style == IconMarkStyle.MdlFilled;
        string data = (kind, filled, mdl) switch
        {
            (IconKind.Home, false, _) => "M 3,11 L 12,3.2 L 21,11 V 20.6 H 14.7 V 13.5 H 9.3 V 20.6 H 3 Z",
            (IconKind.Home, true, true) => "M 2.5,11 L 12,2.8 L 21.5,11 V 21 H 13.8 V 13.2 H 10.2 V 21 H 2.5 Z",
            (IconKind.Home, true, _) => "M 3,11 L 12,3.2 L 21,11 V 20.6 H 14.7 V 13.5 H 9.3 V 20.6 H 3 Z",
            (IconKind.Theme, false, _) => "M 4,7 H 20 M 8,7 A 2,2 0 1 1 7.99,7 M 4,12 H 20 M 15,12 A 2,2 0 1 1 14.99,12 M 4,17 H 20 M 11,17 A 2,2 0 1 1 10.99,17",
            (IconKind.Theme, true, _) => "M 3.5,5.7 H 20.5 V 8.3 H 3.5 Z M 3.5,10.7 H 20.5 V 13.3 H 3.5 Z M 3.5,15.7 H 20.5 V 18.3 H 3.5 Z",
            (IconKind.Texts, false, _) => "M 6,19 L 12,5 L 18,19 M 8.5,13.5 H 15.5",
            (IconKind.Texts, true, _) => "M 12,4.6 L 18.4,19.2 H 15.5 L 13.7,14.4 H 10.3 L 8.5,19.2 H 5.6 Z M 10.7,12.6 H 13.3 L 12,9 Z",
            (IconKind.Colors, _, _) => "M 12,3.2 C 12,3.2 5.2,11 5.2,15.2 A 6.8,6.8 0 0 0 18.8,15.2 C 18.8,11 12,3.2 12,3.2 Z",
            (IconKind.About, _, _) => "M 12,3.2 A 8.8,8.8 0 1 1 11.99,3.2 Z M 12,10.2 V 16.6 M 12,7 V 8.2",
            (IconKind.Marks, false, _) => "M 4,4 H 10 V 10 H 4 Z M 14,4 H 20 V 10 H 14 Z M 4,14 H 10 V 20 H 4 Z M 14,14 H 20 V 20 H 14 Z",
            (IconKind.Marks, true, _) => "M 4,4 H 10.4 V 10.4 H 4 Z M 13.6,4 H 20 V 10.4 H 13.6 Z M 4,13.6 H 10.4 V 20 H 4 Z M 13.6,13.6 H 20 V 20 H 13.6 Z",
            (IconKind.Appearance, _, _) => "M 12,4 V 6.2 M 12,17.8 V 20 M 4,12 H 6.2 M 17.8,12 H 20 M 6.4,6.4 L 7.9,7.9 M 16.1,16.1 L 17.6,17.6 M 6.4,17.6 L 7.9,16.1 M 16.1,7.9 L 17.6,6.4 M 12,8.2 A 3.8,3.8 0 1 1 11.99,8.2 Z",
            (IconKind.Layout, _, _) => "M 3.5,4 H 20.5 V 20 H 3.5 Z M 3.5,10.2 H 20.5 M 10.2,4 V 20 M 16.8,4 V 20",
            (IconKind.Charts, false, _) => "M 5,18 V 11 H 8.2 V 18 Z M 10.4,18 V 6 H 13.6 V 18 Z M 15.8,18 V 13 H 19 V 18 Z M 3.5,20 H 20.5",
            (IconKind.Charts, true, _) => "M 5,18 V 11 H 8.2 V 18 Z M 10.4,18 V 6 H 13.6 V 18 Z M 15.8,18 V 13 H 19 V 18 Z",
            (IconKind.Work, false, _) => "M 8,4 H 10 C 10,2.8 14,2.8 14,4 H 16 V 6 H 8 Z M 7,6 H 17 V 20 H 7 Z",
            (IconKind.Work, true, _) => "M 8,4 H 16 V 6 H 8 Z M 7,6 H 17 V 20 H 7 Z",
            (IconKind.Settings, _, _) => "M 12,8.2 A 3.8,3.8 0 1 1 11.99,8.2 Z M 11,3 H 13 L 13.7,6.2 L 16.6,5.2 L 18.4,8.2 L 15.8,10 L 18.4,11.8 L 16.6,14.8 L 13.7,13.8 L 13,21 H 11 L 10.3,13.8 L 7.4,14.8 L 5.6,11.8 L 8.2,10 L 5.6,8.2 L 7.4,5.2 L 10.3,6.2 Z",
            (IconKind.Author, _, _) => "M 12,4.2 A 3.6,3.6 0 1 1 11.99,4.2 Z M 6.2,20 C 6.2,15.4 8.8,13.2 12,13.2 C 15.2,13.2 17.8,15.4 17.8,20 Z",
            (IconKind.Engineering, false, _) => "M 8,7.5 L 4,12 L 8,16.5 M 16,7.5 L 20,12 L 16,16.5 M 13.4,5.5 L 10.6,18.5",
            (IconKind.Engineering, true, _) => "M 8,7.5 L 4,12 L 8,16.5 L 5.6,12 Z M 16,7.5 L 20,12 L 16,16.5 L 18.4,12 Z",
            (IconKind.Components, false, _) => "M 12,3.2 L 20,7.2 L 12,11.2 L 4,7.2 Z M 4,12 L 12,16 L 20,12 M 4,16.2 L 12,20.2 L 20,16.2",
            (IconKind.Components, true, _) => "M 12,3.2 L 20,7.2 L 12,11.2 L 4,7.2 Z",
            (IconKind.Search, _, _) => "M 10,3 A 7,7 0 1 1 9.99,3 Z M 15.2,15.2 L 21,21",
            (IconKind.Add, _, _) => "M 11,3 H 13 V 11 H 21 V 13 H 13 V 21 H 11 V 13 H 3 V 11 H 11 Z",
            (IconKind.Edit, _, _) => "M 4,16.5 L 15.8,4.7 L 19.3,8.2 L 7.5,20 H 4 Z M 14.8,5.7 L 18.3,9.2",
            (IconKind.Delete, _, _) => "M 5,7 H 19 M 9,7 V 5 H 15 V 7 M 7,7 L 8,20 H 16 L 17,7 M 10,10 V 17 M 14,10 V 17",
            (IconKind.Save, _, _) => "M 4,3 H 17 L 21,7 V 21 H 3 V 4 Z M 7,3 V 9 H 16 V 3 M 7,21 V 14 H 17 V 21",
            (IconKind.Document, _, _) => "M 6,3 H 15 L 20,8 V 21 H 4 V 3 Z M 15,3 V 8 H 20 M 7,12 H 17 M 7,16 H 17",
            (IconKind.Folder, _, _) => "M 3,6 H 10 L 12,8 H 21 V 19 H 3 Z",
            (IconKind.Close, _, _) => "M 5,5 L 19,19 M 19,5 L 5,19",
            (IconKind.Menu, _, _) => "M 4,6 H 20 M 4,12 H 20 M 4,18 H 20",
            (IconKind.ArrowRight, _, _) => "M 4,11 H 18 M 12,5 L 19,12 L 12,19",
            (IconKind.Refresh, _, _) => "M 19,8 A 8,8 0 1 0 20,13 M 19,4 V 9 H 14",
            (IconKind.Print, _, _) => "M 7,8 V 3 H 17 V 8 M 6,17 H 4 V 9 H 20 V 17 H 18 M 7,14 H 17 V 21 H 7 Z M 16,11 H 17",
            (IconKind.Filter, _, _) => "M 3,4 H 21 L 14,12 V 20 L 10,18 V 12 Z",
            (IconKind.More, _, _) => "M 5,12 A 1,1 0 1 1 4.99,12 Z M 12,12 A 1,1 0 1 1 11.99,12 Z M 19,12 A 1,1 0 1 1 18.99,12 Z",
            (IconKind.Check, _, _) => "M 4,12 L 9,17 L 20,6",
            (IconKind.Warning, _, _) => "M 12,3 L 22,21 H 2 Z M 12,9 V 14 M 12,17 V 18",
            (IconKind.Calendar, _, _) => "M 4,5 H 20 V 21 H 4 Z M 8,3 V 7 M 16,3 V 7 M 4,10 H 20 M 8,13 H 10 M 13,13 H 15 M 8,16 H 10",
            (IconKind.Clock, _, _) => "M 12,3 A 9,9 0 1 1 11.99,3 Z M 12,7 V 12 L 16,14",
            (IconKind.Export, _, _) => "M 12,14 V 3 M 7,8 L 12,3 L 17,8 M 4,14 V 20 H 20 V 14",
            (IconKind.Import, _, _) => "M 12,3 V 14 M 7,9 L 12,14 L 17,9 M 4,17 V 21 H 20 V 17",
            (IconKind.Help, _, _) => "M 12,3 A 9,9 0 1 1 11.99,3 Z M 9,9 A 3,3 0 1 1 14,12 C 12,13 12,14 12,15 M 12,18 V 19",
            _ => "M 4,4 H 10.4 V 10.4 H 4 Z M 13.6,4 H 20 V 10.4 H 13.6 Z M 4,13.6 H 10.4 V 20 H 4 Z M 13.6,13.6 H 20 V 20 H 13.6 Z"
        };
        var geometry = Geometry.Parse(data);
        geometry.Freeze();
        return geometry;
    }
}

public readonly record struct IconRecipe(bool Filled, double Stroke, bool Duotone, bool Plate, double Scale);
