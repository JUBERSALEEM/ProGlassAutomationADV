// ╔══════════════════════════════════════════════════════════════════════════╗
// ║  ViewModels/ProformaInvoice/Step5DataGrid/DataGridViewModel.cs           ║
// ║                                                                          ║
// ║  CHAT 20 FINAL — Multi-row selection + persistent Find/Replace.         ║
// ║                                                                          ║
// ║  NEW IN THIS VERSION:                                                    ║
// ║    SEL-01: IsSelected property on GlassItemRow (row highlight state)    ║
// ║    SEL-02: SpecGroup implements 6 new IExcelPasteable methods:          ║
// ║              ClearRowSelection, SelectAllRows, SetRowSelection,          ║
// ║              SelectRowRange, GetSelectedRows, FillDownAcrossRows        ║
// ║    FIND-01: ExecuteOpenFind uses callback pattern (persistent dialog)   ║
// ║    FIND-02: ExecuteOpenFindReplace uses callback pattern                ║
// ║    PRICE-01: ImportedPrice set from spec on FIRST pick, preserved       ║
// ║               thereafter — user can edit BaseRate without losing it     ║
// ║    PRICE-02: AddNewSpecGroup defaults to 0m (spec selection sets price) ║
// ║                                                                          ║
// ║  PRESERVED FROM PREVIOUS:                                                ║
// ║    ✅ Deferred row addition via DispatcherPriority.Loaded                ║
// ║    ✅ AddRows / RemoveRow / FindNext / FindAndReplace / FillDown         ║
// ║    ✅ IsHighlighted + HighlightRow (yellow flash)                        ║
// ║    ✅ Paste engine (cursor-column-aware, AddRangeSilent)                 ║
// ║    ✅ HOTFIX-5 spec autocomplete, HOTFIX-4 glass type auto-switch       ║
// ║    ✅ HOTFIX-1 BaseRate propagation to rows                              ║
// ║    ✅ Global Ctrl+F/H commands on DataGridViewModel                      ║
// ╚══════════════════════════════════════════════════════════════════════════╝

using ProGlassAutomation.Behaviors;
using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.ProformaInvoice.Specifications;
using ProGlassAutomation.Utilities;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step5DataGrid.GrandTotalSummary;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;

namespace ProGlassAutomation.ViewModels.ProformaInvoice.Step5DataGrid
{
    public static class GridColumnIndex
    {
        public const int GlassRef = 0;
        public const int W1 = 1;
        public const int H1 = 2;
        public const int W2 = 3;
        public const int H2 = 4;
        public const int Qty = 5;
        public const int Rate = 6;
    }

    // ════════════════════════════════════════════════════════════════════════
    // CLASS: GlassItemRow
    // ════════════════════════════════════════════════════════════════════════

    public class DataGridViewModel : ViewModelBase
    {
        public string Title { get; } = "SPECIFICATION SUB-GROUPS";
        public string Subtitle { get; } =
            "Specify the number of unique glass specifications in this proforma.";

        public List<string> GlassTypeOptions { get; } =
            new() { "SGU", "DGU", "LAM", "DGU+LAM" };

        public ObservableCollection<SpecGroup> SpecGroups { get; }

        public int TotalSpecGroups => SpecGroups?.Count ?? 0;

        private SpecGroup? _selectedSpecGroup;
        /// <summary>
        /// Bound to the Step 5 tab strip. After Clear()/restore the
        /// TabControl otherwise keeps SelectedIndex = -1, so the grid
        /// looks hidden until the user clicks + ADD SPEC.
        /// </summary>
        public SpecGroup? SelectedSpecGroup
        {
            get => _selectedSpecGroup;
            set
            {
                if (ReferenceEquals(_selectedSpecGroup, value)) return;
                _selectedSpecGroup = value;
                OnPropertyChanged();
            }
        }

        public ICommand AddSpecGroupCommand { get; }
        public ICommand RemoveSpecGroupCommand { get; }
        public ICommand OpenGlobalFindCommand { get; }
        public ICommand OpenGlobalFindReplaceCommand { get; }

        public GrandTotalSummaryViewModel SummaryViewModel { get; }

        public DataGridViewModel()
        {
            SpecGroups = new ObservableCollection<SpecGroup>();
            SpecGroups.CollectionChanged += (_, __) =>
            {
                OnPropertyChanged(nameof(TotalSpecGroups));
                EnsureSelection();
            };

            AddSpecGroupCommand = new RelayCommand(
                _ => AddNewSpecGroup());

            RemoveSpecGroupCommand = new RelayCommand(
                p =>
                {
                    if (p is SpecGroup g)
                    {
                        SpecGroups.Remove(g);
                        RenumberGroups();
                    }
                },
                p => SpecGroups.Count > 1
            );

            // Global Ctrl+F / Ctrl+H — route to first spec group.
            OpenGlobalFindCommand = new RelayCommand(_ =>
            {
                var firstGroup = SpecGroups.FirstOrDefault();
                firstGroup?.OpenFindCommand.Execute(null);
            });

            OpenGlobalFindReplaceCommand = new RelayCommand(_ =>
            {
                var firstGroup = SpecGroups.FirstOrDefault();
                firstGroup?.OpenFindReplaceCommand.Execute(null);
            });

            SummaryViewModel = new GrandTotalSummaryViewModel(SpecGroups);

            AddNewSpecGroup();
        }

        /// <summary>
        /// Fresh invoice: one spec tab, one empty row, selected so the
        /// grid is visible the moment the user reaches Step 5.
        /// </summary>
        public void ResetForNewInvoice()
        {
            SpecGroups.Clear();
            AddNewSpecGroup();
        }

        /// <summary>
        /// Guarantees a visible workspace: at least one spec group, at
        /// least one empty row in any group that has none, sequential
        /// "Spec 1…" names, and a selected tab.
        /// </summary>
        public void EnsureDefaultWorkspace()
        {
            if (SpecGroups.Count == 0)
            {
                AddNewSpecGroup();
                return;
            }

            foreach (var group in SpecGroups)
            {
                if (group.ItemRows.Count == 0)
                    group.AddRowCommand.Execute(null);
            }

            RenumberGroups();
            EnsureSelection();
        }

        /// <summary>
        /// Creates a new spec group with zero defaults.
        /// ImportedPrice and BaseRate stay at 0 until user picks a spec —
        /// the spec's PricePerSqm sets both (ImportedPrice preserved thereafter
        /// even if user manually edits BaseRate).
        /// </summary>
        private void AddNewSpecGroup()
        {
            int number = SpecGroups.Count + 1;
            var group = new SpecGroup
            {
                GroupNumber = number,
                GroupName = $"Spec {number}",
                ImportedPrice = 0m,
                BaseRate = 0m,
                SurchargePercent = 0m,
                SelectedGlassType = "SGU"
            };

            group.AddRowCommand.Execute(null);
            SpecGroups.Add(group);
            SelectedSpecGroup = group;
        }

        private void EnsureSelection()
        {
            if (SelectedSpecGroup != null && SpecGroups.Contains(SelectedSpecGroup))
                return;
            SelectedSpecGroup = SpecGroups.Count > 0 ? SpecGroups[0] : null;
        }

        private void RenumberGroups()
        {
            for (int i = 0; i < SpecGroups.Count; i++)
            {
                SpecGroups[i].GroupNumber = i + 1;
                string name = SpecGroups[i].GroupName ?? "";
                if (string.IsNullOrWhiteSpace(name) ||
                    name.StartsWith("Spec Group ", StringComparison.Ordinal) ||
                    IsGenericSpecName(name))
                    SpecGroups[i].GroupName = $"Spec {i + 1}";
            }
        }

        private static bool IsGenericSpecName(string name)
        {
            if (!name.StartsWith("Spec ", StringComparison.Ordinal))
                return false;
            return int.TryParse(name.Substring(5), out _);
        }
    }
}
