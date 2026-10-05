using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ProGlassAutomation.ViewModels.DeliveryNote.DeliveryNoteDetail
{
    /// <summary>
    /// One spec-group card on the DN items grid.
    /// Grouping is done in the VM — ItemsControl.GroupStyle + Filter
    /// was leaving closed rows on screen.
    /// </summary>
    public class DnSpecGroupViewModel
    {
        public string SpecGroupName { get; }
        public ObservableCollection<DnItemRowViewModel> Items { get; }
        public int ItemCount => Items.Count;

        public DnSpecGroupViewModel(
            string specGroupName,
            IEnumerable<DnItemRowViewModel> items)
        {
            SpecGroupName = string.IsNullOrWhiteSpace(specGroupName)
                ? "SPEC"
                : specGroupName;
            Items = new ObservableCollection<DnItemRowViewModel>(
                items ?? Enumerable.Empty<DnItemRowViewModel>());
        }
    }
}
