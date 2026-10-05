// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/Reports/StatementGenerator/Filter/FilterStep.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: Statement Generator — Wizard Step 1: FILTER
//
// PURPOSE:
//   Code-behind for the Filter step. Intentionally minimal —
//   the step is fully data-driven via bindings to
//   StatementGeneratorViewModel (inherited from parent shell).
//
// DATA CONTEXT:
//   Automatically inherited from parent Shell's DataContext
//   (StatementGeneratorViewModel). No explicit assignment.
// ═══════════════════════════════════════════════════════════════════════

using System.Windows.Controls;

namespace ProGlassAutomation.Views.Reports.StatementGenerator.Filter
{
    public partial class FilterStep : UserControl
    {
        public FilterStep()
        {
            InitializeComponent();
        }
    }
}