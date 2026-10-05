// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Reports/StatementGenerator/StatementGeneratorViewModel.Navigation.cs
// PROJECT: ProGlassAutomation
// ─────────────────────────────────────────────────────────────────────────
// Phase 3.3 — StatementGeneratorViewModel partial split. This part owns the
// Navigation members; fields + constructor live in StatementGeneratorViewModel.cs.
// ═══════════════════════════════════════════════════════════════════════

using PdfSharp.Pdf;
using ProGlassAutomation.Services.Logging;
using PdfSharp.Pdf.IO;
using ProGlassAutomation.Commands;
using ProGlassAutomation.Models.MasterData;
using ProGlassAutomation.Models.Statement;
using ProGlassAutomation.Models.Statement.Reconciliation;
using ProGlassAutomation.Models.Statement.Workflow;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.Statement.Aggregation;
using ProGlassAutomation.Services.Statement.Pdf;
using ProGlassAutomation.Services.Statement.Repository;
using ProGlassAutomation.Services.Statement.Workflow;
using ProGlassAutomation.Utilities;
using ProGlassAutomation.Views.Reports.Workflow;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using ProGlassAutomation.Services.Localization;
using ProGlassAutomation.Services.Dialogs;
using ProGlassAutomation.Services.MasterData.Repository;
using ProGlassAutomation.Services.Database;

namespace ProGlassAutomation.ViewModels.Reports.StatementGenerator
{
    public partial class StatementGeneratorViewModel : ViewModelBase
    {
        // ═════════════════════════════════════════════════════════════════
        //  NAVIGATION NOTIFICATION HELPERS (Chat 22 v3)
        // ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Notifies all filter-dependent properties + Step 2 gate.
        /// Called from filter setters + status item changes.
        /// </summary>
        private void NotifyFilterDependents()
        {
            OnPropertyChanged(nameof(HasFilter));
            OnPropertyChanged(nameof(IsStep1Complete));
            OnPropertyChanged(nameof(CanNavigateToStep2));
        }

        /// <summary>
        /// Notifies ALL step navigation gates + completion states.
        /// Called when IsGenerating flips or wizard state resets.
        /// </summary>
        private void NotifyAllNavigationStates()
        {
            OnPropertyChanged(nameof(IsStep1Complete));
            OnPropertyChanged(nameof(IsStep2Complete));
            OnPropertyChanged(nameof(IsStep3Complete));
            OnPropertyChanged(nameof(IsStep4Complete));
            OnPropertyChanged(nameof(CanNavigateToStep1));
            OnPropertyChanged(nameof(CanNavigateToStep2));
            OnPropertyChanged(nameof(CanNavigateToStep3));
            OnPropertyChanged(nameof(CanNavigateToStep4));
            OnPropertyChanged(nameof(CanGoPrevious));
            OnPropertyChanged(nameof(CanGoNext));
            OnPropertyChanged(nameof(ShowNextButton));
            OnPropertyChanged(nameof(ShowGenerateButton));
            OnPropertyChanged(nameof(ShowFinishButton));
        }

        // ═════════════════════════════════════════════════════════════════
        //  DEBOUNCE
        // ═════════════════════════════════════════════════════════════════

        private void SchedulePreview()
        {
            _debounceTimer.Stop();

            if (!HasFilter)
            {
                PreviewSnapshot = null;
                PreviewVM.UpdateFromSnapshot(null);
                StatusMessage = "";
                return;
            }

            if (!_autoPreviewEnabled)
            {
                StatusMessage = "Filters changed — click Preview to update.";
                return;
            }

            _debounceTimer.Start();
        }

    }
}
