using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Input;

namespace ProGlassAutomation.ThemeStudio;

/// <summary>Local illustrative content only: no persistence, ERP service or database.</summary>
public sealed record ReferenceSample(string Title, string Owner, string Updated, string Status)
{
    public string Severity => Status switch { "Ready" => "Success", "Pending" => "Warning", "Review" => "Information", _ => "Neutral" };
}

public sealed partial class ThemeStudioViewModel
{
    private string referenceFilter = "";
    private ReferenceSample? selectedReference;
    public IReadOnlyList<ReferenceSample> ReferenceItems { get; } =
    [
        new("Atrium façade", "Design team", "Today", "Ready"),
        new("Lobby partitions", "Review team", "Today", "Pending"),
        new("Office screens", "Coordination", "Yesterday", "Review"),
        new("Entrance canopy", "Site team", "Yesterday", "Not verified")
    ];
    public ICollectionView ReferenceView { get; private set; } = null!;
    public string ReferenceFilter
    {
        get => referenceFilter;
        set { referenceFilter = value; ReferenceView.Refresh(); Changed(nameof(ReferenceFilter)); Changed(nameof(ReferenceSummary)); }
    }
    public ReferenceSample? SelectedReference
    {
        get => selectedReference;
        set { selectedReference = value; Changed(nameof(SelectedReference)); CommandManager.InvalidateRequerySuggested(); }
    }
    public string ReferenceSummary => ReferenceView.IsEmpty
        ? "No work items match. Clear the search to show all sample work."
        : $"{ReferenceView.Cast<ReferenceSample>().Count()} work items · Sample data only";
    public ICommand ReviewReference { get; private set; } = null!;
    public ICommand ClearPreview { get; private set; } = null!;
    public ICommand ShowHelp { get; private set; } = null!;

    private void InitializeReferenceSamples()
    {
        ReferenceView = CollectionViewSource.GetDefaultView(ReferenceItems);
        ReferenceView.Filter = item => item is ReferenceSample row &&
            $"{row.Title} {row.Owner} {row.Status}".Contains(referenceFilter, StringComparison.OrdinalIgnoreCase);
        SelectedReference = ReferenceItems[0];
        ReviewReference = new DelegateCommand(() => Feedback = $"Review selected: {SelectedReference!.Title} (preview only).", () => SelectedReference is not null);
        ClearPreview = new DelegateCommand(() => { SampleName = ""; Feedback = "Preview name cleared. No saved data was changed. Reset restores the sample."; });
        ShowHelp = new DelegateCommand(() => Feedback = "Reference guide: Tab moves focus, arrows change selections, Alt+Down opens the theme selector. Changes are local previews.");
    }
}
