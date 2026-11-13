namespace ProGlassAutomation.Customization;

/// <summary>
/// One-editor façade over <see cref="CustomizationStore"/>. Preview stays on ThemeService.
/// Save advances the baseline; failed writes leave the previous file.
/// </summary>
public sealed class ThemeEditSession : IDisposable
{
    private static ThemeEditSession? current;
    private SavedCustomization baseline;
    private bool disposed;

    private ThemeEditSession(SavedCustomization opening) => baseline = opening ?? throw new ArgumentNullException(nameof(opening));

    public SavedCustomization Baseline => baseline;
    public bool HasSaved { get; private set; }

    public static ThemeEditSession Begin(SavedCustomization opening)
    {
        if (current is { disposed: false })
            throw new InvalidOperationException("A theme editing session is already open.");
        return current = new ThemeEditSession(opening);
    }

    public void Save(SavedCustomization draft)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        CustomizationStore.Save(draft);
        baseline = draft;
        HasSaved = true;
    }

    public SavedCustomization Revert()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        return baseline;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        if (ReferenceEquals(current, this)) current = null;
    }
}
