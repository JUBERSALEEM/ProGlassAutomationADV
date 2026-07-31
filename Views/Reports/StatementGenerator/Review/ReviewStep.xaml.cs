// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/Reports/StatementGenerator/Review/ReviewStep.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: Statement Generator — Wizard Step 4: REVIEW (Chat 21 v3)
//
// PURPOSE:
//   Code-behind for the Review step. Minimal — fully data-driven
//   via bindings to StatementGeneratorViewModel.
//
// DATA CONTEXT:
//   Automatically inherited from parent Shell's DataContext.
// ═══════════════════════════════════════════════════════════════════════

using System.Windows.Controls;

namespace ProGlassAutomation.Views.Reports.StatementGenerator.Review
{
    public partial class ReviewStep : UserControl
    {
        public ReviewStep()
        {
            InitializeComponent();
        }
    }
}