// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Reports/StatementGenerator/StatementGeneratorViewModel.WizardState.cs
// PROJECT: ProGlassAutomation
// ─────────────────────────────────────────────────────────────────────────
// Phase 3.3 — StatementGeneratorViewModel partial split. This part owns the
// WizardState members; fields + constructor live in StatementGeneratorViewModel.cs.
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
        //  WIZARD STATE — CLICKABLE STEPPER (Chat 22 v3)
        // ═════════════════════════════════════════════════════════════════

        private int _currentStep = 1;
        public int CurrentStep
        {
            get => _currentStep;
            set
            {
                if (_currentStep == value) return;
                _currentStep = value;
                OnPropertyChanged();
                // [CHAT 22 v3] All step-position-dependent properties
                OnPropertyChanged(nameof(CanGoPrevious));
                OnPropertyChanged(nameof(CanGoNext));
                OnPropertyChanged(nameof(ShowNextButton));
                OnPropertyChanged(nameof(ShowGenerateButton));
                OnPropertyChanged(nameof(ShowFinishButton));
                OnPropertyChanged(nameof(CurrentStepTitle));
                OnPropertyChanged(nameof(IsStep1Active));
                OnPropertyChanged(nameof(IsStep2Active));
                OnPropertyChanged(nameof(IsStep3Active));
                OnPropertyChanged(nameof(IsStep4Active));
                ProGlassAutomation.Utilities.AsyncUtility.InvalidateCommandsThrottled();
            }
        }

        // ── Step completion — reflects ACTUAL state, not position (v3) ──
        public bool IsStep1Complete => HasFilter;
        public bool IsStep2Complete => PreviewVM.HasSelection;
        public bool IsStep3Complete => IsLastWorkflowComplete;
        public bool IsStep4Complete => HasLastPdf;

        // ── Which step badge is currently active (v3) ────────────────────
        public bool IsStep1Active => _currentStep == 1;
        public bool IsStep2Active => _currentStep == 2;
        public bool IsStep3Active => _currentStep == 3;
        public bool IsStep4Active => _currentStep == 4;

        // ── Navigation gates — can user jump to this step? (v3) ─────────
        public bool CanNavigateToStep1 => !_isGenerating;
        public bool CanNavigateToStep2 => HasFilter && !_isGenerating;
        public bool CanNavigateToStep3 => PreviewVM.HasSelection && !_isGenerating;
        public bool CanNavigateToStep4 => HasLastPdf && !_isGenerating;

        // ── Footer nav gates ─────────────────────────────────────────────
        public bool CanGoPrevious => _currentStep > 1 && !_isGenerating;
        public bool CanGoNext => _currentStep < 4 && !_isGenerating;

        public bool ShowNextButton => _currentStep == 1 && !_isGenerating;
        public bool ShowGenerateButton => _currentStep == 2 && !_isGenerating;
        public bool ShowFinishButton => _currentStep == 4 && !_isGenerating;

        public string CurrentStepTitle => _currentStep switch
        {
            1 => "Filter",
            2 => "Preview",
            3 => "Execute",
            4 => "Review",
            _ => "Generator"
        };

        public ICommand FinishCommand { get; private set; } = null!;

        // ── Commands ─────────────────────────────────────────────────────
        public ICommand NextStepCommand { get; private set; } = null!;
        public ICommand PreviousStepCommand { get; private set; } = null!;
        public ICommand GoToStepCommand { get; private set; } = null!;   // [CHAT 22 v3]

        /// <summary>
        /// Public entry point called by Shell code-behind when Step 3 activates.
        /// [CHAT 22 v3] Added guard: don't re-run if already completed successfully.
        /// </summary>
        public async Task RunPipelineAsync()
        {
            if (_isGenerating) return;

            // [CHAT 22 v3] Don't re-run if we already have a successful result
            if (IsLastWorkflowComplete && HasLastPdf)
            {
                AppLogService.Debug("[VM] Skipping pipeline re-run — already complete");
                return;
            }

            await RunGenerateAsync();

            if (IsLastWorkflowComplete || HasLastPdf)
            {
                ProGlassAutomation.ViewModels.Shared.UiDispatch.Run(() =>
                {
                    CurrentStep = 4;
                });
            }
        }

        // ═════════════════════════════════════════════════════════════════
        //  COMMANDS
        // ═════════════════════════════════════════════════════════════════

        public ICommand PreviewCommand { get; }
        public ICommand GenerateCommand { get; }
        public ICommand GenerateAgainCommand { get; }
        public ICommand ClearFiltersCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand SelectAllCommand { get; }
        public ICommand DeselectAllCommand { get; }
        public ICommand DownloadLastPdfCommand { get; }
        public ICommand CopyPdfPathCommand { get; }
        public ICommand ShareLastPdfCommand { get; }
        public ICommand SelectMergedTabCommand { get; }
        public ICommand SelectBalanceTabCommand { get; }
        public ICommand SelectAlertsTabCommand { get; }

    }
}
